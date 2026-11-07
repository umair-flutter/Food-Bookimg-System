using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FoodBooking.Core.Entities;

namespace FoodBooking.Core.Services;

public record PaymentResult(bool Success, string? TransactionId, string? Error);

/// <summary>
/// Clover integration:
///  * POS: pushes every web order into the merchant's Clover POS as an open order and fires a print event
///    so it prints on the kitchen / order printer.
///  * E-commerce gateway: charges a card token produced by the Clover iframe (card data never touches our server).
/// </summary>
public class CloverService(SettingsService settings, IHttpClientFactory http)
{
    public static string ApiBase(SiteSetting s) => s.CloverSandbox ? "https://apisandbox.dev.clover.com" : "https://api.clover.com";
    public static string EcommBase(SiteSetting s) => s.CloverSandbox ? "https://scl-sandbox.dev.clover.com" : "https://scl.clover.com";
    public static string SdkUrl(SiteSetting s) => s.CloverSandbox ? "https://checkout.sandbox.dev.clover.com/sdk.js" : "https://checkout.clover.com/sdk.js";

    public static bool PosConfigured(SiteSetting s) =>
        s.CloverEnabled && !string.IsNullOrWhiteSpace(s.CloverMerchantId) && !string.IsNullOrWhiteSpace(s.CloverApiToken);

    public static bool GatewayConfigured(SiteSetting s) =>
        !string.IsNullOrWhiteSpace(s.CloverEcommercePublicKey) && !string.IsNullOrWhiteSpace(s.CloverEcommercePrivateKey);

    private static long Cents(decimal v) => (long)Math.Round(v * 100m, MidpointRounding.AwayFromZero);

    /// <summary>Creates the order in Clover POS. Returns the Clover order id.</summary>
    public async Task<PaymentResult> PushOrderAsync(Order order)
    {
        var s = await settings.GetAsync();
        if (!PosConfigured(s)) return new PaymentResult(false, null, "Clover POS is not enabled/configured.");

        var client = http.CreateClient();
        client.BaseAddress = new Uri(ApiBase(s));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.CloverApiToken);
        var m = s.CloverMerchantId;

        var note = new StringBuilder();
        note.Append($"WEB ORDER {order.OrderNumber} | {order.OrderType} | {order.CustomerName} {order.Phone}");
        if (order.ScheduledFor.HasValue) note.Append($" | FOR {order.ScheduledFor:ddd h:mm tt}");
        note.Append($" | {order.PaymentMethod.PaymentText()} ({order.PaymentStatus})");
        if (!string.IsNullOrWhiteSpace(order.Notes)) note.Append($" | NOTE: {order.Notes}");

        var create = await client.PostAsJsonAsync($"/v3/merchants/{m}/orders", new
        {
            state = "open",
            title = $"Web #{order.OrderNumber}",
            note = note.ToString(),
            manualTransaction = false
        });
        var createBody = await create.Content.ReadAsStringAsync();
        if (!create.IsSuccessStatusCode) return new PaymentResult(false, null, $"Create order failed ({(int)create.StatusCode}): {createBody}");
        var cloverOrderId = JsonNode.Parse(createBody)?["id"]?.GetValue<string>();
        if (string.IsNullOrEmpty(cloverOrderId)) return new PaymentResult(false, null, "Clover did not return an order id.");

        // One Clover line item per unit (Clover's model), with modifiers / notes as the line note.
        var items = new List<object>();
        foreach (var it in order.Items)
            for (int i = 0; i < it.Quantity; i++)
            {
                var cloverItemId = it.Product?.CloverItemId;
                items.Add(string.IsNullOrWhiteSpace(cloverItemId)
                    ? new { name = it.ProductName, price = Cents(it.UnitPrice), note = it.OptionsText }
                    : (object)new { item = new { id = cloverItemId }, name = it.ProductName, price = Cents(it.UnitPrice), note = it.OptionsText });
            }
        if (order.DeliveryCharge > 0) items.Add(new { name = "Delivery charge", price = Cents(order.DeliveryCharge) });
        if (order.Tip > 0) items.Add(new { name = "Tip", price = Cents(order.Tip) });

        var li = await client.PostAsJsonAsync($"/v3/merchants/{m}/orders/{cloverOrderId}/bulk_line_items", new { items });
        if (!li.IsSuccessStatusCode)
            return new PaymentResult(false, cloverOrderId, $"Line items failed ({(int)li.StatusCode}): {await li.Content.ReadAsStringAsync()}");

        if (order.Discount > 0)
            await client.PostAsJsonAsync($"/v3/merchants/{m}/orders/{cloverOrderId}/discounts",
                new { name = $"Coupon {order.CouponCode}", amount = -Cents(order.Discount) });

        if (s.CloverPrintToKitchen)
        {
            var pr = await client.PostAsJsonAsync($"/v3/merchants/{m}/print_event", new { orderRef = new { id = cloverOrderId } });
            if (!pr.IsSuccessStatusCode)
                return new PaymentResult(true, cloverOrderId, $"Order created but kitchen print failed ({(int)pr.StatusCode}).");
        }
        return new PaymentResult(true, cloverOrderId, null);
    }

    /// <summary>Charges a Clover card token (from the Clover hosted iframe).</summary>
    public async Task<PaymentResult> ChargeAsync(string token, decimal amount, string currency, string description, string? email)
    {
        var s = await settings.GetAsync();
        if (!GatewayConfigured(s)) return new PaymentResult(false, null, "Card payments are not configured.");
        var client = http.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, EcommBase(s) + "/v1/charges")
        {
            Content = JsonContent.Create(new
            {
                amount = Cents(amount),
                currency = currency.ToLowerInvariant(),
                source = token,
                description,
                receipt_email = email
            })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.CloverEcommercePrivateKey);
        req.Headers.Add("idempotency-key", Guid.NewGuid().ToString());
        var res = await client.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        var json = SafeParse(body);
        if (res.IsSuccessStatusCode && json?["paid"]?.GetValue<bool>() == true)
            return new PaymentResult(true, json["id"]?.GetValue<string>(), null);
        var msg = json?["error"]?["message"]?.GetValue<string>() ?? json?["message"]?.GetValue<string>() ?? "Card was declined.";
        return new PaymentResult(false, null, msg);
    }

    private static JsonNode? SafeParse(string s) { try { return JsonNode.Parse(s); } catch { return null; } }
}

/// <summary>PayPal Checkout (Orders v2 REST API).</summary>
public class PayPalService(SettingsService settings, IHttpClientFactory http)
{
    public static bool Configured(SiteSetting s) => !string.IsNullOrWhiteSpace(s.PaypalClientId) && !string.IsNullOrWhiteSpace(s.PaypalSecret);
    private static string Base(SiteSetting s) => s.PaypalSandbox ? "https://api-m.sandbox.paypal.com" : "https://api-m.paypal.com";

    private async Task<(HttpClient client, SiteSetting s)> AuthClientAsync()
    {
        var s = await settings.GetAsync();
        var client = http.CreateClient();
        client.BaseAddress = new Uri(Base(s));
        var tokenReq = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" })
        };
        tokenReq.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{s.PaypalClientId}:{s.PaypalSecret}")));
        var res = await client.SendAsync(tokenReq);
        res.EnsureSuccessStatusCode();
        var token = JsonNode.Parse(await res.Content.ReadAsStringAsync())?["access_token"]?.GetValue<string>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, s);
    }

    public async Task<PaymentResult> CreateOrderAsync(decimal amount, string reference)
    {
        var (client, s) = await AuthClientAsync();
        var res = await client.PostAsJsonAsync("/v2/checkout/orders", new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = reference,
                    description = $"{s.CompanyName} food order",
                    amount = new { currency_code = s.CurrencyCode, value = amount.ToString("0.00", CultureInfo.InvariantCulture) }
                }
            }
        });
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) return new PaymentResult(false, null, "PayPal error: " + body);
        return new PaymentResult(true, JsonNode.Parse(body)?["id"]?.GetValue<string>(), null);
    }

    /// <summary>Captures an approved PayPal order. Returns the capture id and the captured amount.</summary>
    public async Task<(PaymentResult result, decimal captured)> CaptureAsync(string paypalOrderId)
    {
        var (client, _) = await AuthClientAsync();
        var res = await client.PostAsync($"/v2/checkout/orders/{Uri.EscapeDataString(paypalOrderId)}/capture",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) return (new PaymentResult(false, null, "PayPal capture failed: " + body), 0);
        var json = JsonNode.Parse(body);
        if (json?["status"]?.GetValue<string>() != "COMPLETED") return (new PaymentResult(false, null, "PayPal payment not completed."), 0);
        var cap = json["purchase_units"]?[0]?["payments"]?["captures"]?[0];
        var value = decimal.Parse(cap?["amount"]?["value"]?.GetValue<string>() ?? "0", CultureInfo.InvariantCulture);
        return (new PaymentResult(true, cap?["id"]?.GetValue<string>() ?? paypalOrderId, null), value);
    }
}
