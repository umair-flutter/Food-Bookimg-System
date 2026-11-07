using System.Globalization;
using System.Text;
using FoodBooking.Admin.Models;
using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodBooking.Admin.Controllers;

public class OrdersController(AppDbContext db, OrderService orders, CloverService clover, NotificationService notify, SettingsService settings) : AdminController
{
    private IQueryable<Order> Filter(OrderFilter f)
    {
        var q = db.Orders.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var t = f.Q.Trim();
            q = q.Where(o => o.OrderNumber.Contains(t) || o.CustomerName.Contains(t) || o.Email.Contains(t) || o.Phone.Contains(t));
        }
        if (f.Status.HasValue) q = q.Where(o => o.Status == f.Status);
        if (f.Payment.HasValue) q = q.Where(o => o.PaymentMethod == f.Payment);
        if (f.PaymentStatus.HasValue) q = q.Where(o => o.PaymentStatus == f.PaymentStatus);
        if (f.Type.HasValue) q = q.Where(o => o.OrderType == f.Type);
        if (f.From.HasValue) q = q.Where(o => o.CreatedAt >= f.From.Value.Date);
        if (f.To.HasValue) q = q.Where(o => o.CreatedAt < f.To.Value.Date.AddDays(1));
        return q;
    }

    public async Task<IActionResult> Index(OrderFilter f)
    {
        var q = Filter(f);
        var list = new PagedList<Order> { Page = Math.Max(1, f.Page), PageSize = 25, Total = await q.CountAsync() };
        list.Items = await q.OrderByDescending(o => o.CreatedAt).Skip((list.Page - 1) * list.PageSize).Take(list.PageSize).ToListAsync();
        ViewBag.Filter = f;
        return View(list);
    }

    public async Task<IActionResult> Details(int id)
    {
        var o = await db.Orders.Include(x => x.Items).Include(x => x.History).Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        if (!o.KitchenAcknowledged) { o.KitchenAcknowledged = true; await db.SaveChangesAsync(); }
        return View(o);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, string? note, bool notifyCustomer = true, string? returnTo = null)
    {
        if (await orders.UpdateStatusAsync(id, status, note, notifyCustomer))
            Success($"Order status changed to {status.StatusText()}." + (notifyCustomer ? " Customer notified." : ""));
        if (Request.Headers.XRequestedWith == "XMLHttpRequest") return Json(new { ok = true });
        return returnTo == "kitchen" ? RedirectToAction(nameof(Kitchen)) : RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePayment(int id, PaymentStatus paymentStatus, string? transactionId)
    {
        var o = await db.Orders.FindAsync(id);
        if (o == null) return NotFound();
        o.PaymentStatus = paymentStatus;
        if (!string.IsNullOrWhiteSpace(transactionId)) o.TransactionId = transactionId.Trim();
        o.UpdatedAt = DateTime.UtcNow;
        o.History.Add(new OrderStatusHistory { Status = o.Status, Note = $"Payment marked as {paymentStatus}." });
        await db.SaveChangesAsync();
        Success("Payment status updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SendToClover(int id)
    {
        var o = await db.Orders.Include(x => x.Items).ThenInclude(i => i.Product).FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        var r = await clover.PushOrderAsync(o);
        o.CloverOrderId = r.TransactionId ?? o.CloverOrderId;
        o.CloverSyncError = r.Error;
        await db.SaveChangesAsync();
        var s = await settings.GetAsync();
        await notify.LogAsync(NotificationChannel.CloverPos, s.CloverMerchantId, $"Order {o.OrderNumber} → Clover (manual)", r.TransactionId, r.Success, r.Error);
        if (r.Success) Success("Order sent to Clover POS" + (r.Error != null ? $" ({r.Error})" : "."));
        else Error("Clover: " + r.Error);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ResendConfirmation(int id)
    {
        var o = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        var s = await settings.GetAsync();
        var ok = await notify.SendEmailAsync(o.Email, $"Your order #{o.OrderNumber} - {s.CompanyName}",
            EmailTemplates.Invoice(o, s, "Your order invoice", "Here is a copy of your order invoice."));
        if (ok) Success("Invoice emailed to " + o.Email); else Error("Email could not be sent — see Logs > Notifications.");
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Invoice(int id)
    {
        var o = await db.Orders.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        ViewBag.Settings = await settings.GetAsync();
        return View(o);
    }

    /// <summary>Kitchen display: live board of active orders with sound alerts.</summary>
    public async Task<IActionResult> Kitchen()
    {
        var active = await db.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Preparing
                        || o.Status == OrderStatus.ReadyForPickup || o.Status == OrderStatus.OutForDelivery)
            .OrderBy(o => o.ScheduledFor ?? o.CreatedAt).ToListAsync();
        if (Request.Query.ContainsKey("partial")) return PartialView("_KitchenBoard", active);
        return View(active);
    }

    [HttpPost]
    public async Task<IActionResult> Accept(int id)
    {
        await orders.UpdateStatusAsync(id, OrderStatus.Confirmed, "Order accepted by restaurant.", true);
        return Request.Headers.XRequestedWith == "XMLHttpRequest" ? Json(new { ok = true }) : RedirectToAction(nameof(Kitchen));
    }

    public async Task<IActionResult> Export(OrderFilter f)
    {
        var rows = await Filter(f).OrderByDescending(o => o.CreatedAt).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Order,Date,Customer,Email,Phone,Type,Status,Payment,PaymentStatus,SubTotal,Discount,Tax,Delivery,Tip,Total,Coupon,CloverOrderId");
        foreach (var o in rows)
            sb.AppendLine(string.Join(",", new[]
            {
                o.OrderNumber, o.CreatedAt.ToString("yyyy-MM-dd HH:mm"), o.CustomerName, o.Email, o.Phone, o.OrderType.ToString(),
                o.Status.StatusText(), o.PaymentMethod.PaymentText(), o.PaymentStatus.ToString(),
                N(o.SubTotal), N(o.Discount), N(o.Tax), N(o.DeliveryCharge), N(o.Tip), N(o.Total), o.CouponCode ?? "", o.CloverOrderId ?? ""
            }.Select(Csv)));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", $"orders-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    internal static string N(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);
    internal static string Csv(string? s)
    {
        s ??= "";
        if (s.Length > 0 && "=+-@".Contains(s[0])) s = "'" + s; // prevent spreadsheet formula injection
        return s.Contains(',') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
