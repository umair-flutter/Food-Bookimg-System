using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodBooking.Admin.Controllers;

public class PagesController(AppDbContext db) : AdminController
{
    public async Task<IActionResult> Index() => View(await db.CmsPages.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync());
    public IActionResult Create() => View("Form", new CmsPage());
    public async Task<IActionResult> Edit(int id) => await db.CmsPages.FindAsync(id) is { } p ? View("Form", p) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Save(CmsPage m)
    {
        ModelState.Remove(nameof(CmsPage.Slug));
        m.Slug = Slug.Create(string.IsNullOrWhiteSpace(m.Slug) ? m.Title : m.Slug);
        if (await db.CmsPages.AnyAsync(c => c.Slug == m.Slug && c.Id != m.Id)) ModelState.AddModelError(nameof(m.Slug), "Slug already in use.");
        if (!ModelState.IsValid) return View("Form", m);
        var p = m.Id == 0 ? new CmsPage() : await db.CmsPages.FindAsync(m.Id);
        if (p == null) return NotFound();
        p.Title = m.Title; p.Slug = m.Slug; p.Content = m.Content; p.MetaTitle = m.MetaTitle; p.MetaDescription = m.MetaDescription;
        p.ShowInHeader = m.ShowInHeader; p.ShowInFooter = m.ShowInFooter; p.SortOrder = m.SortOrder; p.IsActive = m.IsActive; p.UpdatedAt = DateTime.UtcNow;
        if (m.Id == 0) db.CmsPages.Add(p);
        await db.SaveChangesAsync();
        Success($"Page \"{p.Title}\" saved. It is available at /page/{p.Slug}");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (await db.CmsPages.FindAsync(id) is { } p) { db.CmsPages.Remove(p); await db.SaveChangesAsync(); Success("Page deleted."); }
        return RedirectToAction(nameof(Index));
    }
}

public class BannersController(AppDbContext db, FileStorage storage) : AdminController
{
    public async Task<IActionResult> Index(BannerPosition? position)
    {
        var q = db.Banners.AsNoTracking();
        if (position.HasValue) q = q.Where(b => b.Position == position);
        ViewBag.Position = position;
        return View(await q.OrderBy(b => b.Position).ThenBy(b => b.SortOrder).ToListAsync());
    }

    public IActionResult Create(BannerPosition position = BannerPosition.HomeSlider) => View("Form", new Banner { Position = position });
    public async Task<IActionResult> Edit(int id) => await db.Banners.FindAsync(id) is { } b ? View("Form", b) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Save(Banner m, IFormFile? image)
    {
        var url = await TryUpload(storage, image, "banners", "image");
        if (m.Id == 0 && url == null && string.IsNullOrEmpty(m.ImageUrl)) ModelState.AddModelError("image", "Please upload a banner image.");
        if (m.EndDate.HasValue && m.StartDate.HasValue && m.EndDate < m.StartDate) ModelState.AddModelError(nameof(m.EndDate), "End date must be after start date.");
        if (!ModelState.IsValid) return View("Form", m);
        var b = m.Id == 0 ? new Banner() : await db.Banners.FindAsync(m.Id);
        if (b == null) return NotFound();
        b.Title = m.Title; b.SubTitle = m.SubTitle; b.LinkUrl = m.LinkUrl; b.ButtonText = m.ButtonText; b.Position = m.Position;
        b.SortOrder = m.SortOrder; b.IsActive = m.IsActive; b.StartDate = m.StartDate; b.EndDate = m.EndDate;
        if (url != null) { storage.Delete(b.ImageUrl); b.ImageUrl = url; }
        if (m.Id == 0) db.Banners.Add(b);
        await db.SaveChangesAsync();
        Success("Banner saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (await db.Banners.FindAsync(id) is { } b)
        {
            db.Banners.Remove(b);
            await db.SaveChangesAsync();
            storage.Delete(b.ImageUrl);
            Success("Banner deleted.");
        }
        return RedirectToAction(nameof(Index));
    }
}

public class CouponsController(AppDbContext db) : AdminController
{
    public async Task<IActionResult> Index() => View(await db.Coupons.AsNoTracking().OrderByDescending(c => c.IsActive).ThenBy(c => c.Code).ToListAsync());
    public IActionResult Create() => View("Form", new Coupon());
    public async Task<IActionResult> Edit(int id) => await db.Coupons.FindAsync(id) is { } c ? View("Form", c) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Save(Coupon m)
    {
        m.Code = (m.Code ?? "").Trim().ToUpperInvariant();
        if (await db.Coupons.AnyAsync(c => c.Code == m.Code && c.Id != m.Id)) ModelState.AddModelError(nameof(m.Code), "Code already exists.");
        if (m.DiscountType == DiscountType.Percentage && (m.Value <= 0 || m.Value > 100)) ModelState.AddModelError(nameof(m.Value), "Percentage must be between 1 and 100.");
        if (m.Value <= 0) ModelState.AddModelError(nameof(m.Value), "Value must be greater than zero.");
        if (!ModelState.IsValid) return View("Form", m);
        var c = m.Id == 0 ? new Coupon() : await db.Coupons.FindAsync(m.Id);
        if (c == null) return NotFound();
        c.Code = m.Code; c.Description = m.Description; c.DiscountType = m.DiscountType; c.Value = m.Value; c.MinOrderAmount = m.MinOrderAmount;
        c.MaxDiscount = m.MaxDiscount; c.StartsAt = m.StartsAt; c.ExpiresAt = m.ExpiresAt; c.UsageLimit = m.UsageLimit; c.IsActive = m.IsActive;
        if (m.Id == 0) db.Coupons.Add(c);
        await db.SaveChangesAsync();
        Success($"Coupon {c.Code} saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (await db.Coupons.FindAsync(id) is { } c) { db.Coupons.Remove(c); await db.SaveChangesAsync(); Success("Coupon deleted."); }
        return RedirectToAction(nameof(Index));
    }
}

/// <summary>Holiday setup + weekly opening hours.</summary>
public class HolidaysController(AppDbContext db) : AdminController
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Hours = await db.BusinessHours.OrderBy(h => h.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)h.DayOfWeek).ToListAsync();
        return View(await db.Holidays.AsNoTracking().OrderByDescending(h => h.Date >= DateTime.UtcNow.Date).ThenBy(h => h.Date).ToListAsync());
    }

    public IActionResult Create() => View("Form", new Holiday { Date = DateTime.Today.AddDays(7) });
    public async Task<IActionResult> Edit(int id) => await db.Holidays.FindAsync(id) is { } h ? View("Form", h) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Save(Holiday m)
    {
        if (!m.IsClosedAllDay && (m.OpenTime == null || m.CloseTime == null)) ModelState.AddModelError(nameof(m.OpenTime), "Enter the special opening hours, or tick 'Closed all day'.");
        if (await db.Holidays.AnyAsync(h => h.Date == m.Date.Date && h.Id != m.Id)) ModelState.AddModelError(nameof(m.Date), "A holiday already exists on this date.");
        if (!ModelState.IsValid) return View("Form", m);
        var h = m.Id == 0 ? new Holiday() : await db.Holidays.FindAsync(m.Id);
        if (h == null) return NotFound();
        h.Date = m.Date.Date; h.Title = m.Title; h.Message = m.Message; h.IsClosedAllDay = m.IsClosedAllDay;
        h.OpenTime = m.IsClosedAllDay ? null : m.OpenTime; h.CloseTime = m.IsClosedAllDay ? null : m.CloseTime;
        if (m.Id == 0) db.Holidays.Add(h);
        await db.SaveChangesAsync();
        Success("Holiday saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (await db.Holidays.FindAsync(id) is { } h) { db.Holidays.Remove(h); await db.SaveChangesAsync(); Success("Holiday deleted."); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SaveHours(List<BusinessHour> hours)
    {
        var existing = await db.BusinessHours.ToListAsync();
        foreach (var d in Enum.GetValues<DayOfWeek>())
        {
            var posted = hours.FirstOrDefault(h => h.DayOfWeek == d);
            if (posted == null) continue;
            var row = existing.FirstOrDefault(h => h.DayOfWeek == d);
            if (row == null) db.BusinessHours.Add(row = new BusinessHour { DayOfWeek = d });
            row.OpenTime = posted.OpenTime; row.CloseTime = posted.CloseTime; row.IsClosed = posted.IsClosed;
        }
        await db.SaveChangesAsync();
        Success("Opening hours saved.");
        return RedirectToAction(nameof(Index));
    }
}

public class MessagesController(AppDbContext db) : AdminController
{
    public async Task<IActionResult> Index() => View(await db.ContactMessages.AsNoTracking().OrderByDescending(m => m.CreatedAt).Take(300).ToListAsync());

    public async Task<IActionResult> Details(int id)
    {
        var m = await db.ContactMessages.FindAsync(id);
        if (m == null) return NotFound();
        if (!m.IsRead) { m.IsRead = true; await db.SaveChangesAsync(); }
        return View(m);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (await db.ContactMessages.FindAsync(id) is { } m) { db.ContactMessages.Remove(m); await db.SaveChangesAsync(); Success("Message deleted."); }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Subscribers() => View(await db.NewsletterSubscribers.AsNoTracking().OrderByDescending(s => s.CreatedAt).ToListAsync());

    public async Task<IActionResult> ExportSubscribers()
    {
        var list = await db.NewsletterSubscribers.AsNoTracking().OrderBy(s => s.Email).ToListAsync();
        var csv = "Email,SubscribedAt\n" + string.Join("\n", list.Select(s => $"{OrdersController.Csv(s.Email)},{s.CreatedAt:yyyy-MM-dd}"));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "subscribers.csv");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSubscriber(int id)
    {
        if (await db.NewsletterSubscribers.FindAsync(id) is { } s) { db.NewsletterSubscribers.Remove(s); await db.SaveChangesAsync(); Success("Subscriber removed."); }
        return RedirectToAction(nameof(Subscribers));
    }

    public async Task<IActionResult> Logs(NotificationChannel? channel, int page = 1)
    {
        var q = db.NotificationLogs.AsNoTracking();
        if (channel.HasValue) q = q.Where(l => l.Channel == channel);
        var list = new Models.PagedList<NotificationLog> { Page = Math.Max(1, page), PageSize = 30, Total = await q.CountAsync() };
        list.Items = await q.OrderByDescending(l => l.Id).Skip((list.Page - 1) * list.PageSize).Take(list.PageSize).ToListAsync();
        ViewBag.Channel = channel;
        return View(list);
    }

    public async Task<IActionResult> Log(int id) => await db.NotificationLogs.FindAsync(id) is { } l ? View(l) : NotFound();
}
