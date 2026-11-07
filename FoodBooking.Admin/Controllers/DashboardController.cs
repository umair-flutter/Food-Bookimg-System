using FoodBooking.Admin.Models;
using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodBooking.Admin.Controllers;

public class DashboardController(AppDbContext db, SettingsService settings) : AdminController
{
    public async Task<IActionResult> Index()
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var valid = db.Orders.Where(o => o.Status != OrderStatus.Cancelled);
        var m = new DashboardModel
        {
            OrdersToday = await db.Orders.CountAsync(o => o.CreatedAt >= today),
            RevenueToday = await valid.Where(o => o.CreatedAt >= today).SumAsync(o => (decimal?)o.Total) ?? 0,
            PendingOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending),
            OrdersMonth = await db.Orders.CountAsync(o => o.CreatedAt >= monthStart),
            RevenueMonth = await valid.Where(o => o.CreatedAt >= monthStart).SumAsync(o => (decimal?)o.Total) ?? 0,
            Customers = await db.Customers.CountAsync(),
            Products = await db.Products.CountAsync(p => p.IsActive),
            UnreadMessages = await db.ContactMessages.CountAsync(x => !x.IsRead),
            RecentOrders = await db.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAt).Take(8).ToListAsync(),
            LowStock = await db.Products.AsNoTracking().Where(p => p.TrackStock && p.StockQuantity <= 5).OrderBy(p => p.StockQuantity).Take(6).ToListAsync(),
            ByStatus = (await db.Orders.GroupBy(o => o.Status).Select(g => new { g.Key, C = g.Count() }).ToListAsync())
                .ToDictionary(x => x.Key.StatusText(), x => x.C)
        };

        var from = today.AddDays(-13);
        var daily = await valid.Where(o => o.CreatedAt >= from)
            .GroupBy(o => o.CreatedAt.Date).Select(g => new { Day = g.Key, C = g.Count(), R = g.Sum(o => o.Total) }).ToListAsync();
        for (var d = from; d <= today; d = d.AddDays(1))
        {
            var x = daily.FirstOrDefault(v => v.Day == d);
            m.Last14Days.Add((d.ToString("MMM d"), x?.C ?? 0, x?.R ?? 0));
        }

        var top = await db.OrderItems.Where(i => i.Order!.Status != OrderStatus.Cancelled && i.Order.CreatedAt >= today.AddDays(-30))
            .GroupBy(i => i.ProductName).Select(g => new { Name = g.Key, Q = g.Sum(i => i.Quantity), R = g.Sum(i => i.LineTotal) })
            .OrderByDescending(x => x.Q).Take(6).ToListAsync();
        m.TopProducts = top.Select(t => (t.Name, t.Q, t.R)).ToList();

        var s = await settings.GetAsync();
        if (s.PaymentTestMode) m.SetupWarnings.Add("Payments are in TEST MODE — card and PayPal payments are simulated. Configure them in Settings → Payments.");
        if (!s.EmailEnabled) m.SetupWarnings.Add("Email sending is off — order confirmations are only logged. Configure SMTP in Settings → Email & SMS.");
        if (!s.SmsEnabled) m.SetupWarnings.Add("SMS alerts are off. Configure Twilio in Settings → Email & SMS.");
        if (!s.CloverEnabled) m.SetupWarnings.Add("Clover POS integration is off. Configure it in Settings → Clover POS.");
        var admin = await db.AdminUsers.FindAsync(AdminId);
        if (admin != null && Passwords.Verify(admin.PasswordHash, "software"))
            m.SetupWarnings.Insert(0, "You are still using the default admin password. Please change it now.");
        return View(m);
    }

    /// <summary>Polled by every admin page: count of new (unacknowledged) orders for the bell + kitchen alert sound.</summary>
    [HttpGet]
    public async Task<IActionResult> Poll()
    {
        var newOrders = await db.Orders.AsNoTracking().Where(o => !o.KitchenAcknowledged && o.Status == OrderStatus.Pending)
            .OrderByDescending(o => o.Id).Select(o => new { o.Id, o.OrderNumber, o.CustomerName, o.Total }).Take(10).ToListAsync();
        return Json(new { count = newOrders.Count, latestId = newOrders.FirstOrDefault()?.Id ?? 0, orders = newOrders });
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
