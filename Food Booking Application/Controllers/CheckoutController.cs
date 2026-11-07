using System.Text.Json;
using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;
using Food_Booking_Application.Infrastructure;
using Food_Booking_Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Food_Booking_Application.Controllers;

[Route("checkout")]
public class CheckoutController(
    AppDbContext db,
    CartStore store,
    PricingService pricing,
    OrderService orders,
    StoreHoursService hours,
    SettingsService settingsService,
    CloverService clover,
    PayPalService paypal,
    ILogger<CheckoutController> log) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var s = await settingsService.GetAsync();
        var cart = store.Get();
        if (cart.Lines.Count == 0) return RedirectToAction("Index", "Cart");
        if (!s.EnableGuestCheckout && User.CustomerId() == null)
            return RedirectToAction("Login", "Account", new { returnUrl = "/checkout" });

        var input = new CheckoutInput
        {
            OrderType = s.EnableDelivery ? OrderType.Delivery : OrderType.Pickup,
            PaymentMethod = s.EnableCashOnDelivery ? PaymentMethod.CashOnDelivery : s.EnableCreditCard ? PaymentMethod.CreditCard : PaymentMethod.PayPal
        };
        if (User.CustomerId() is int id && await db.Customers.FindAsync(id) is { } c)
        {
            input.FirstName = c.FirstName; input.LastName = c.LastName; input.Email = c.Email; input.Phone = c.Phone ?? "";
            input.Address = c.Address; input.City = c.City; input.PostalCode = c.PostalCode;
        }
        return View(await BuildVm(input, new()));
    }

    [HttpPost("")]
    public async Task<IActionResult> Place(CheckoutInput input)
    {
        var s = await settingsService.GetAsync();
        var cart = store.Get();
        var summary = await pricing.PriceAsync(cart, input.OrderType);

        var errors = ModelState.IsValid ? new List<string>() : ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
        var (ruleErrors, scheduled) = await orders.ValidateAsync(input, summary);
        errors.AddRange(ruleErrors);
        if (!s.EnableGuestCheckout && User.CustomerId() == null) errors.Add("Please log in to place an order.");
        if (input.PaymentMethod == PaymentMethod.PayPal && !s.PaymentTestMode)
            errors.Add("Please use the PayPal button to complete payment.");
        if (errors.Count > 0) return View("Index", await BuildVm(input, errors.Distinct().ToList()));

        // Payment
        var status = PaymentStatus.Unpaid;
        string? txn = null;
        if (input.PaymentMethod != PaymentMethod.CashOnDelivery)
        {
            if (s.PaymentTestMode)
            {
                status = PaymentStatus.Paid;
                txn = "TEST-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(input.CardToken))
                    return View("Index", await BuildVm(input, ["Please enter your card details."]));
                var charge = await clover.ChargeAsync(input.CardToken, summary.Total, s.CurrencyCode, $"{s.CompanyName} web order", input.Email);
                if (!charge.Success)
                    return View("Index", await BuildVm(input, ["Payment failed: " + charge.Error]));
                status = PaymentStatus.Paid;
                txn = charge.TransactionId;
            }
        }

        return await CreateAndFinish(input, summary, scheduled, status, txn);
    }

    [HttpPost("paypal/create")]
    public async Task<IActionResult> PayPalCreate(CheckoutInput input)
    {
        var s = await settingsService.GetAsync();
        input.PaymentMethod = PaymentMethod.PayPal;
        var summary = await pricing.PriceAsync(store.Get(), input.OrderType);
        var errors = ModelState.IsValid ? new List<string>() : ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
        errors.AddRange((await orders.ValidateAsync(input, summary)).errors);
        if (errors.Count > 0) return Json(new { ok = false, errors = errors.Distinct() });
        if (!PayPalService.Configured(s)) return Json(new { ok = false, errors = new[] { "PayPal is not configured." } });

        try
        {
            var r = await paypal.CreateOrderAsync(summary.Total, "WEB-" + DateTime.UtcNow.Ticks);
            if (!r.Success) return Json(new { ok = false, errors = new[] { "Could not start PayPal payment." } });
            HttpContext.Session.SetString("pp:" + r.TransactionId, JsonSerializer.Serialize(input));
            return Json(new { ok = true, id = r.TransactionId });
        }
        catch (Exception ex)
        {
            log.LogError(ex, "PayPal create failed");
            return Json(new { ok = false, errors = new[] { "Could not connect to PayPal." } });
        }
    }

    [HttpPost("paypal/capture")]
    public async Task<IActionResult> PayPalCapture(string paypalOrderId)
    {
        var json = HttpContext.Session.GetString("pp:" + paypalOrderId);
        if (json == null) return Json(new { ok = false, errors = new[] { "Payment session expired. Please try again." } });
        var input = JsonSerializer.Deserialize<CheckoutInput>(json)!;
        var summary = await pricing.PriceAsync(store.Get(), input.OrderType);
        var (errors, scheduled) = await orders.ValidateAsync(input, summary);
        if (errors.Count > 0) return Json(new { ok = false, errors });

        var (result, captured) = await paypal.CaptureAsync(paypalOrderId);
        if (!result.Success) return Json(new { ok = false, errors = new[] { result.Error } });
        HttpContext.Session.Remove("pp:" + paypalOrderId);
        if (captured + 0.01m < summary.Total)
        {
            log.LogError("PayPal captured {Captured} but order total is {Total} ({Id})", captured, summary.Total, paypalOrderId);
            return Json(new { ok = false, errors = new[] { "Payment amount mismatch. Please contact us with reference " + result.TransactionId } });
        }
        var action = await CreateAndFinish(input, summary, scheduled, PaymentStatus.Paid, result.TransactionId);
        return action is RedirectResult rr ? Json(new { ok = true, redirect = rr.Url }) : Json(new { ok = false, errors = new[] { "Order could not be created." } });
    }

    private async Task<IActionResult> CreateAndFinish(CheckoutInput input, CartSummary summary, DateTime? scheduled, PaymentStatus status, string? txn)
    {
        Order order;
        try
        {
            order = await orders.CreateOrderAsync(input, summary, User.CustomerId(), scheduled, status, txn);
        }
        catch (Exception ex)
        {
            if (status == PaymentStatus.Paid) log.LogError(ex, "Payment {Txn} taken but order creation failed", txn);
            else log.LogError(ex, "Order creation failed");
            var msg = ex is CheckoutException ? ex.Message : "Sorry, we could not place your order. Please try again.";
            if (status == PaymentStatus.Paid) msg += $" Your payment reference is {txn} — please contact us so we can help.";
            return View("Index", await BuildVm(input, [msg]));
        }
        store.Clear();
        await orders.AfterOrderPlacedAsync(order.Id);
        return Redirect($"/order/invoice?number={order.OrderNumber}&key={order.AccessKey}&placed=1");
    }

    private async Task<CheckoutViewModel> BuildVm(CheckoutInput input, List<string> errors)
    {
        var s = await settingsService.GetAsync();
        ViewData["Title"] = "Checkout";
        return new CheckoutViewModel
        {
            Input = input,
            Summary = await pricing.PriceAsync(store.Get(), input.OrderType),
            Settings = s,
            Slots = await hours.GetTimeSlotsAsync(3),
            IsOpenNow = await hours.IsOpenAtAsync(await hours.NowAsync()),
            LoggedIn = User.CustomerId() != null,
            Errors = errors,
            CardGatewayLive = !s.PaymentTestMode && CloverService.GatewayConfigured(s),
            PaypalLive = !s.PaymentTestMode && PayPalService.Configured(s)
        };
    }
}
