using System.Linq.Expressions;
using System.Text;
using FoodBooking.Admin.Models;
using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodBooking.Admin.Controllers;

public class CustomersController(AppDbContext db) : AdminController
{
    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.FirstName.Contains(q) || (c.LastName != null && c.LastName.Contains(q)) || c.Email.Contains(q) || (c.Phone != null && c.Phone.Contains(q)));
        var list = new PagedList<Customer> { Page = Math.Max(1, page), Total = await query.CountAsync() };
        list.Items = await query.OrderByDescending(c => c.CreatedAt).Skip((list.Page - 1) * list.PageSize).Take(list.PageSize).ToListAsync();
        var ids = list.Items.Select(c => c.Id).ToList();
        ViewBag.Stats = await db.Orders.Where(o => o.CustomerId != null && ids.Contains(o.CustomerId.Value) && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => o.CustomerId!.Value).Select(g => new { g.Key, Count = g.Count(), Total = g.Sum(o => o.Total) })
            .ToDictionaryAsync(x => x.Key, x => (x.Count, x.Total));
        ViewBag.Q = q;
        return View(list);
    }

    public async Task<IActionResult> Details(int id)
    {
        var c = await db.Customers.AsNoTracking().Include(x => x.Orders.OrderByDescending(o => o.CreatedAt)).FirstOrDefaultAsync(x => x.Id == id);
        return c == null ? NotFound() : View(c);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var c = await db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        c.IsActive = !c.IsActive;
        await db.SaveChangesAsync();
        Success($"{c.FullName} has been {(c.IsActive ? "activated" : "blocked")}.");
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Export()
    {
        var list = await db.Customers.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        var sb = new StringBuilder("Id,FirstName,LastName,Email,Phone,Address,City,PostalCode,Active,Registered\n");
        foreach (var c in list)
            sb.AppendLine(string.Join(",", new[] { c.Id.ToString(), c.FirstName, c.LastName, c.Email, c.Phone, c.Address, c.City, c.PostalCode, c.IsActive.ToString(), c.CreatedAt.ToString("yyyy-MM-dd") }.Select(OrdersController.Csv)));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", "customers.csv");
    }
}

public class ReportsController(AppDbContext db) : AdminController
{
    /// <summary>Order / sales report over a date range.</summary>
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string groupBy = "day")
    {
        var f = (from ?? DateTime.UtcNow.Date.AddDays(-29)).Date;
        var t = (to ?? DateTime.UtcNow.Date).Date;
        var q = db.Orders.AsNoTracking().Where(o => o.CreatedAt >= f && o.CreatedAt < t.AddDays(1));
        var orders = await q.ToListAsync();
        var valid = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();

        ViewBag.From = f; ViewBag.To = t; ViewBag.GroupBy = groupBy;
        ViewBag.TotalOrders = orders.Count;
        ViewBag.Cancelled = orders.Count(o => o.Status == OrderStatus.Cancelled);
        ViewBag.Revenue = valid.Sum(o => o.Total);
        ViewBag.Discounts = valid.Sum(o => o.Discount);
        ViewBag.Tax = valid.Sum(o => o.Tax);
        ViewBag.Delivery = valid.Sum(o => o.DeliveryCharge);
        ViewBag.Tips = valid.Sum(o => o.Tip);
        ViewBag.AvgOrder = valid.Count > 0 ? valid.Average(o => o.Total) : 0m;
        ViewBag.ByPayment = valid.GroupBy(o => o.PaymentMethod.PaymentText()).Select(g => (g.Key, g.Count(), g.Sum(o => o.Total))).ToList();
        ViewBag.ByType = valid.GroupBy(o => o.OrderType.ToString()).Select(g => (g.Key, g.Count(), g.Sum(o => o.Total))).ToList();
        ViewBag.ByStatus = orders.GroupBy(o => o.Status.StatusText()).Select(g => (g.Key, g.Count(), g.Sum(o => o.Total))).ToList();

        Func<Order, string> key = groupBy switch
        {
            "month" => o => o.CreatedAt.ToString("yyyy-MM"),
            "week" => o => o.CreatedAt.Date.AddDays(-(((int)o.CreatedAt.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd"),
            _ => o => o.CreatedAt.ToString("yyyy-MM-dd")
        };
        ViewBag.Rows = valid.GroupBy(key).OrderBy(g => g.Key)
            .Select(g => (g.Key, g.Count(), g.Sum(o => o.SubTotal), g.Sum(o => o.Discount), g.Sum(o => o.Tax), g.Sum(o => o.DeliveryCharge), g.Sum(o => o.Total))).ToList();
        return View();
    }

    public async Task<IActionResult> Products(DateTime? from, DateTime? to)
    {
        var f = (from ?? DateTime.UtcNow.Date.AddDays(-29)).Date;
        var t = (to ?? DateTime.UtcNow.Date).Date;
        ViewBag.From = f; ViewBag.To = t;
        var rows = await db.OrderItems.AsNoTracking()
            .Where(i => i.Order!.Status != OrderStatus.Cancelled && i.Order.CreatedAt >= f && i.Order.CreatedAt < t.AddDays(1))
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new { g.Key.ProductName, Qty = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.LineTotal), Orders = g.Select(i => i.OrderId).Distinct().Count() })
            .OrderByDescending(x => x.Revenue).ToListAsync();
        ViewBag.Rows = rows.Select(r => (r.ProductName, r.Qty, r.Revenue, r.Orders)).ToList();
        return View();
    }
}

public class SettingsController(AppDbContext db, SettingsService settings, FileStorage storage, NotificationService notify) : AdminController
{
    public static readonly (string key, string title, string icon)[] Sections =
    [
        ("logo", "Logo Setup", "bi-image"),
        ("company", "Company Setup", "bi-building"),
        ("seo", "SEO Meta Tags", "bi-search"),
        ("social", "Social Media", "bi-share"),
        ("ordering", "Ordering & Delivery", "bi-truck"),
        ("payments", "Payments", "bi-credit-card"),
        ("clover", "Clover POS", "bi-shop-window"),
        ("notifications", "Email & SMS", "bi-envelope-paper"),
    ];

    private static readonly Dictionary<string, Expression<Func<SiteSetting, object?>>[]> Fields = new()
    {
        ["company"] = [x => x.CompanyName, x => x.Tagline, x => x.Address, x => x.Phone, x => x.Email, x => x.AboutText, x => x.MapEmbedUrl, x => x.FooterText, x => x.AnnouncementText, x => x.TimeZoneId, x => x.FrontSiteUrl],
        ["seo"] = [x => x.MetaTitle, x => x.MetaDescription, x => x.MetaKeywords, x => x.HeadScripts],
        ["social"] = [x => x.FacebookUrl, x => x.InstagramUrl, x => x.TwitterUrl, x => x.YoutubeUrl, x => x.LinkedinUrl, x => x.TiktokUrl, x => x.WhatsappNumber],
        ["ordering"] = [x => x.CurrencySymbol, x => x.CurrencyCode, x => x.TaxPercent, x => x.DeliveryCharge, x => x.FreeDeliveryAbove, x => x.MinOrderAmount, x => x.EnableDelivery, x => x.EnablePickup, x => x.EnableGuestCheckout, x => x.EnableTips],
        ["payments"] = [x => x.EnableCashOnDelivery, x => x.EnableCreditCard, x => x.EnablePaypal, x => x.PaymentTestMode, x => x.PaypalClientId, x => x.PaypalSandbox, x => x.CloverEcommercePublicKey],
        ["clover"] = [x => x.CloverEnabled, x => x.CloverSandbox, x => x.CloverMerchantId, x => x.CloverPrintToKitchen],
        ["notifications"] = [x => x.EmailEnabled, x => x.SmtpHost, x => x.SmtpPort, x => x.SmtpUseSsl, x => x.SmtpUser, x => x.SmtpFromEmail, x => x.AdminAlertEmail, x => x.SmsEnabled, x => x.TwilioAccountSid, x => x.TwilioFromNumber, x => x.AdminAlertPhone],
    };

    public async Task<IActionResult> Index(string section = "company")
    {
        if (Sections.All(s => s.key != section)) return NotFound();
        ViewBag.Section = section;
        return View(await settings.GetForEditAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Save(string section, IFormFile? logo, IFormFile? footerLogo, IFormFile? favicon,
        string? paypalSecret, string? cloverApiToken, string? cloverPrivateKey, string? smtpPassword, string? twilioAuthToken,
        bool removeLogo = false, bool removeFooterLogo = false, bool removeFavicon = false)
    {
        var s = await settings.GetForEditAsync();
        if (section == "logo")
        {
            async Task Replace(IFormFile? file, Func<string?> get, Action<string?> set, bool remove, string field)
            {
                var url = await TryUpload(storage, file, "branding", field);
                if (url != null) { storage.Delete(get()); set(url); }
                else if (remove) { storage.Delete(get()); set(null); }
            }
            await Replace(logo, () => s.LogoUrl, v => s.LogoUrl = v, removeLogo, "logo");
            await Replace(footerLogo, () => s.FooterLogoUrl, v => s.FooterLogoUrl = v, removeFooterLogo, "footerLogo");
            await Replace(favicon, () => s.FaviconUrl, v => s.FaviconUrl = v, removeFavicon, "favicon");
            if (!ModelState.IsValid) { ViewBag.Section = section; return View("Index", s); }
        }
        else if (Fields.TryGetValue(section, out var fields))
        {
            if (!await TryUpdateModelAsync(s, "", fields))
            {
                ViewBag.Section = section;
                return View("Index", s);
            }
            if (section == "company")
            {
                try { TimeZoneInfo.FindSystemTimeZoneById(s.TimeZoneId); }
                catch { ModelState.AddModelError(nameof(s.TimeZoneId), "Unknown time zone."); ViewBag.Section = section; return View("Index", s); }
            }
            // Secrets are only replaced when a new value is typed (they are never echoed back to the browser)
            if (!string.IsNullOrWhiteSpace(paypalSecret)) s.PaypalSecret = paypalSecret.Trim();
            if (!string.IsNullOrWhiteSpace(cloverPrivateKey)) s.CloverEcommercePrivateKey = cloverPrivateKey.Trim();
            if (!string.IsNullOrWhiteSpace(cloverApiToken)) s.CloverApiToken = cloverApiToken.Trim();
            if (!string.IsNullOrWhiteSpace(smtpPassword)) s.SmtpPassword = smtpPassword;
            if (!string.IsNullOrWhiteSpace(twilioAuthToken)) s.TwilioAuthToken = twilioAuthToken.Trim();
        }
        else return NotFound();

        await db.SaveChangesAsync();
        settings.Invalidate();
        Success("Settings saved. Changes appear on the website within a minute.");
        return RedirectToAction(nameof(Index), new { section });
    }

    [HttpPost]
    public async Task<IActionResult> TestEmail(string to)
    {
        settings.Invalidate();
        var s = await settings.GetAsync();
        var ok = await notify.SendEmailAsync(to, $"Test email from {s.CompanyName}", "<p>Your email settings are working. 🎉</p>");
        if (ok) Success("Test email sent to " + to); else Error("Test email failed — see Logs > Notifications for the error.");
        return RedirectToAction(nameof(Index), new { section = "notifications" });
    }

    [HttpPost]
    public async Task<IActionResult> TestSms(string to)
    {
        settings.Invalidate();
        var s = await settings.GetAsync();
        var ok = await notify.SendSmsAsync(to, $"Test SMS from {s.CompanyName}. Your SMS settings are working.");
        if (ok) Success("Test SMS sent to " + to); else Error("Test SMS failed — see Logs > Notifications for the error.");
        return RedirectToAction(nameof(Index), new { section = "notifications" });
    }
}
