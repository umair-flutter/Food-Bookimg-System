using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FoodBooking.Core.Services;

public class CheckoutInput
{
    [Required, StringLength(60), Display(Name = "First name")] public string FirstName { get; set; } = "";
    [StringLength(60), Display(Name = "Last name")] public string? LastName { get; set; }
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Required, Phone, StringLength(30)] public string Phone { get; set; } = "";
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(80)] public string? City { get; set; }
    [StringLength(20), Display(Name = "Postal code")] public string? PostalCode { get; set; }
    [StringLength(500), Display(Name = "Order notes")] public string? Notes { get; set; }
    public OrderType OrderType { get; set; }
    /// <summary>"asap" or a store-local time "yyyy-MM-ddTHH:mm".</summary>
    public string? When { get; set; } = "asap";
    public PaymentMethod PaymentMethod { get; set; }
    public string? CardToken { get; set; }
    public bool SaveAddress { get; set; } = true;
    public bool AcceptTerms { get; set; }
}

/// <summary>A checkout problem whose message is safe to show to the customer.</summary>
public class CheckoutException(string message) : Exception(message);

public class OrderService(
    AppDbContext db,
    SettingsService settings,
    StoreHoursService hours,
    NotificationService notify,
    CloverService clover,
    ILogger<OrderService> log)
{
    /// <summary>Business-rule validation for checkout. Returns error messages (empty when OK) and the parsed scheduled time.</summary>
    public async Task<(List<string> errors, DateTime? scheduledLocal)> ValidateAsync(CheckoutInput input, CartSummary summary)
    {
        var s = await settings.GetAsync();
        var errors = new List<string>();
        if (summary.Lines.Count == 0) errors.Add("Your cart is empty.");
        if (summary.HasProblems) errors.Add("Some items in your cart are unavailable. Please review your cart.");
        if (summary.BelowMinimum) errors.Add($"Minimum order amount is {s.MinOrderAmount.Money(s.CurrencySymbol)}.");
        if (input.OrderType == OrderType.Delivery && !s.EnableDelivery) errors.Add("Delivery is currently not available.");
        if (input.OrderType == OrderType.Pickup && !s.EnablePickup) errors.Add("Pickup is currently not available.");
        if (input.OrderType == OrderType.Delivery && (string.IsNullOrWhiteSpace(input.Address) || string.IsNullOrWhiteSpace(input.City)))
            errors.Add("Delivery address and city are required for delivery orders.");
        if (!input.AcceptTerms) errors.Add("Please accept the terms and conditions.");

        var methodEnabled = input.PaymentMethod switch
        {
            PaymentMethod.CashOnDelivery => s.EnableCashOnDelivery,
            PaymentMethod.CreditCard => s.EnableCreditCard && (s.PaymentTestMode || CloverService.GatewayConfigured(s)),
            PaymentMethod.PayPal => s.EnablePaypal && (s.PaymentTestMode || PayPalService.Configured(s)),
            _ => false
        };
        if (!methodEnabled) errors.Add("Selected payment method is not available.");

        DateTime? scheduled = null;
        if (!string.IsNullOrWhiteSpace(input.When) && input.When != "asap")
        {
            if (!DateTime.TryParse(input.When, out var t)) errors.Add("Invalid order time.");
            else
            {
                scheduled = t;
                var slots = await hours.GetTimeSlotsAsync(7);
                if (!slots.Contains(t)) errors.Add("The selected time is no longer available. Please choose another time.");
            }
        }
        else if (!await hours.IsOpenAtAsync(await hours.NowAsync()))
        {
            errors.Add("We are closed right now. Please schedule your order for a later time.");
        }
        return (errors, scheduled);
    }

    public async Task<Order> CreateOrderAsync(CheckoutInput input, CartSummary summary, int? customerId, DateTime? scheduledLocal,
        PaymentStatus paymentStatus, string? transactionId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var order = new Order
        {
            OrderNumber = await NewOrderNumberAsync(),
            CustomerId = customerId,
            IsGuest = customerId == null,
            CustomerName = $"{input.FirstName} {input.LastName}".Trim(),
            Email = input.Email.Trim(),
            Phone = input.Phone.Trim(),
            Address = input.OrderType == OrderType.Delivery ? input.Address?.Trim() : null,
            City = input.OrderType == OrderType.Delivery ? input.City?.Trim() : null,
            PostalCode = input.OrderType == OrderType.Delivery ? input.PostalCode?.Trim() : null,
            Notes = input.Notes?.Trim(),
            OrderType = input.OrderType,
            ScheduledFor = scheduledLocal,
            SubTotal = summary.SubTotal,
            Discount = summary.Discount,
            CouponCode = summary.Coupon?.Code,
            Tax = summary.Tax,
            DeliveryCharge = summary.DeliveryCharge,
            Tip = summary.Tip,
            Total = summary.Total,
            PaymentMethod = input.PaymentMethod,
            PaymentStatus = paymentStatus,
            TransactionId = transactionId,
            Status = OrderStatus.Pending
        };
        foreach (var l in summary.Lines)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = l.Product.Id,
                ProductName = l.Product.Name,
                ImageUrl = l.Product.MainImage,
                OptionsText = string.IsNullOrEmpty(l.OptionsText) ? null : l.OptionsText,
                UnitPrice = l.UnitPrice,
                Quantity = l.Line.Quantity,
                LineTotal = l.LineTotal
            });
        }
        order.History.Add(new OrderStatusHistory { Status = OrderStatus.Pending, Note = "Order placed on website." });
        db.Orders.Add(order);

        // Stock + sold counters
        var ids = summary.Lines.Select(l => l.Product.Id).Distinct().ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync();
        foreach (var p in products)
        {
            var qty = summary.Lines.Where(l => l.Product.Id == p.Id).Sum(l => l.Line.Quantity);
            p.SoldCount += qty;
            if (p.TrackStock)
            {
                if (p.StockQuantity < qty) throw new CheckoutException($"Sorry, {p.Name} just went out of stock.");
                p.StockQuantity -= qty;
            }
        }

        if (summary.Coupon != null)
        {
            var c = await db.Coupons.FirstAsync(x => x.Id == summary.Coupon.Id);
            c.UsedCount++;
        }

        if (customerId != null && input.SaveAddress && input.OrderType == OrderType.Delivery)
        {
            var cust = await db.Customers.FindAsync(customerId.Value);
            if (cust != null)
            {
                cust.Address = input.Address; cust.City = input.City; cust.PostalCode = input.PostalCode;
                if (string.IsNullOrWhiteSpace(cust.Phone)) cust.Phone = input.Phone;
            }
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return order;
    }

    /// <summary>Post-order processing: Clover POS push + kitchen print, invoice email, SMS alerts. Never throws.</summary>
    public async Task AfterOrderPlacedAsync(int orderId)
    {
        var order = await db.Orders.Include(o => o.Items).ThenInclude(i => i.Product).FirstAsync(o => o.Id == orderId);
        var s = await settings.GetAsync();

        try
        {
            if (CloverService.PosConfigured(s))
            {
                var r = await clover.PushOrderAsync(order);
                order.CloverOrderId = r.TransactionId;
                order.CloverSyncError = r.Error;
                await notify.LogAsync(NotificationChannel.CloverPos, s.CloverMerchantId, $"Order {order.OrderNumber} → Clover",
                    r.TransactionId, r.Success, r.Error);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Clover push failed for {Order}", order.OrderNumber);
            order.CloverSyncError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            await db.SaveChangesAsync();
        }

        try
        {
            await notify.SendEmailAsync(order.Email, $"Order confirmation #{order.OrderNumber} - {s.CompanyName}",
                EmailTemplates.Invoice(order, s, "Thank you for your order!", "We have received your order and will start preparing it shortly."));
            if (!string.IsNullOrWhiteSpace(s.AdminAlertEmail))
                await notify.SendEmailAsync(s.AdminAlertEmail, $"New order #{order.OrderNumber} ({order.Total.Money(s.CurrencySymbol)})",
                    EmailTemplates.Invoice(order, s, "New order received", $"{order.CustomerName} placed a new {order.OrderType} order."));

            await notify.SendSmsAsync(order.Phone,
                $"{s.CompanyName}: Thanks {order.CustomerName.Split(' ')[0]}! Order #{order.OrderNumber} received. Total {order.Total.Money(s.CurrencySymbol)}.");
            if (!string.IsNullOrWhiteSpace(s.AdminAlertPhone))
                await notify.SendSmsAsync(s.AdminAlertPhone,
                    $"NEW ORDER #{order.OrderNumber} {order.OrderType} {order.Total.Money(s.CurrencySymbol)} - {order.CustomerName} {order.Phone}");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Notifications failed for {Order}", order.OrderNumber);
        }
    }

    public async Task<bool> UpdateStatusAsync(int orderId, OrderStatus status, string? note, bool notifyCustomer)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null || order.Status == status) return false;

        if (status == OrderStatus.Cancelled && order.Status != OrderStatus.Cancelled)
        {
            var ids = order.Items.Where(i => i.ProductId != null).Select(i => i.ProductId!.Value).ToList();
            var products = await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync();
            foreach (var p in products)
            {
                var qty = order.Items.Where(i => i.ProductId == p.Id).Sum(i => i.Quantity);
                p.SoldCount = Math.Max(0, p.SoldCount - qty);
                if (p.TrackStock) p.StockQuantity += qty;
            }
        }
        if (status == OrderStatus.Delivered && order.PaymentMethod == PaymentMethod.CashOnDelivery)
            order.PaymentStatus = PaymentStatus.Paid;

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        order.KitchenAcknowledged = true;
        order.History.Add(new OrderStatusHistory { Status = status, Note = note });
        await db.SaveChangesAsync();

        if (notifyCustomer)
        {
            var s = await settings.GetAsync();
            var msg = $"Your order #{order.OrderNumber} is now: {status.StatusText()}." + (string.IsNullOrWhiteSpace(note) ? "" : $" {note}");
            await notify.SendEmailAsync(order.Email, $"Order #{order.OrderNumber} - {status.StatusText()}",
                EmailTemplates.Invoice(order, s, $"Order {status.StatusText()}", msg));
            await notify.SendSmsAsync(order.Phone, $"{s.CompanyName}: {msg}");
        }
        return true;
    }

    private async Task<string> NewOrderNumberAsync()
    {
        while (true)
        {
            var n = DateTime.UtcNow.ToString("yyMMdd") + Random.Shared.Next(1000, 9999);
            if (!await db.Orders.AnyAsync(o => o.OrderNumber == n)) return n;
        }
    }
}

public static class EmailTemplates
{
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    public static string Invoice(Order o, SiteSetting s, string heading, string intro)
    {
        var c = s.CurrencySymbol;
        var sb = new StringBuilder();
        sb.Append($"""
<div style="font-family:Arial,Helvetica,sans-serif;max-width:640px;margin:auto;color:#222">
  <div style="background:#e63946;color:#fff;padding:20px 24px;border-radius:8px 8px 0 0">
    <h2 style="margin:0">{H(s.CompanyName)}</h2><div style="opacity:.9">{H(s.Tagline)}</div>
  </div>
  <div style="border:1px solid #eee;border-top:0;padding:24px;border-radius:0 0 8px 8px">
    <h3 style="margin-top:0">{H(heading)}</h3>
    <p>{H(intro)}</p>
    <table style="width:100%;font-size:14px;margin-bottom:16px">
      <tr><td><b>Order #</b> {H(o.OrderNumber)}</td><td style="text-align:right"><b>Status:</b> {o.Status.StatusText()}</td></tr>
      <tr><td><b>Type:</b> {o.OrderType}{(o.ScheduledFor.HasValue ? " · " + o.ScheduledFor.Value.ToString("ddd, MMM d h:mm tt") : " · ASAP")}</td>
          <td style="text-align:right"><b>Payment:</b> {o.PaymentMethod.PaymentText()} ({o.PaymentStatus})</td></tr>
      <tr><td colspan="2"><b>Customer:</b> {H(o.CustomerName)} · {H(o.Phone)} · {H(o.Email)}</td></tr>
""");
        if (o.OrderType == OrderType.Delivery)
            sb.Append($"""<tr><td colspan="2"><b>Deliver to:</b> {H(o.Address)}, {H(o.City)} {H(o.PostalCode)}</td></tr>""");
        sb.Append("""
    </table>
    <table style="width:100%;border-collapse:collapse;font-size:14px">
      <tr style="background:#f6f6f6"><th style="text-align:left;padding:8px">Item</th><th style="padding:8px">Qty</th><th style="text-align:right;padding:8px">Price</th><th style="text-align:right;padding:8px">Total</th></tr>
""");
        foreach (var i in o.Items)
            sb.Append($"""
      <tr><td style="padding:8px;border-bottom:1px solid #eee">{H(i.ProductName)}{(string.IsNullOrEmpty(i.OptionsText) ? "" : $"<br><small style='color:#777'>{H(i.OptionsText)}</small>")}</td>
      <td style="text-align:center;border-bottom:1px solid #eee">{i.Quantity}</td>
      <td style="text-align:right;padding:8px;border-bottom:1px solid #eee">{i.UnitPrice.Money(c)}</td>
      <td style="text-align:right;padding:8px;border-bottom:1px solid #eee">{i.LineTotal.Money(c)}</td></tr>
""");
        void Row(string label, decimal v, bool bold = false) =>
            sb.Append($"<tr><td colspan='3' style='text-align:right;padding:6px 8px'>{(bold ? "<b>" : "")}{label}{(bold ? "</b>" : "")}</td><td style='text-align:right;padding:6px 8px'>{(bold ? "<b>" : "")}{v.Money(c)}{(bold ? "</b>" : "")}</td></tr>");
        Row("Subtotal", o.SubTotal);
        if (o.Discount > 0) Row($"Discount ({H(o.CouponCode)})", -o.Discount);
        if (o.Tax > 0) Row("Tax", o.Tax);
        if (o.DeliveryCharge > 0) Row("Delivery", o.DeliveryCharge);
        if (o.Tip > 0) Row("Tip", o.Tip);
        Row("Total", o.Total, true);
        sb.Append($"""
    </table>
    <p style="margin-top:24px;font-size:13px;color:#666">{H(s.Address)} · {H(s.Phone)} · {H(s.Email)}</p>
    <p style="font-size:13px"><a href="{H(s.FrontSiteUrl)}/order/track?number={H(o.OrderNumber)}&key={H(o.AccessKey)}">Track your order / view invoice</a></p>
  </div>
</div>
""");
        return sb.ToString();
    }
}
