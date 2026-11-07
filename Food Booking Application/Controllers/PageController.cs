using System.Net;
using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;
using Food_Booking_Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Food_Booking_Application.Controllers;

public class PageController(AppDbContext db, NotificationService notify, SettingsService settings) : Controller
{
    [Route("page/{slug}")]
    public async Task<IActionResult> Show(string slug)
    {
        var p = await db.CmsPages.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive);
        if (p == null) return NotFound();
        ViewData["Title"] = p.MetaTitle ?? p.Title;
        ViewData["MetaDescription"] = p.MetaDescription;
        return View(p);
    }

    [HttpGet("contact")]
    public IActionResult Contact()
    {
        ViewData["Title"] = "Contact Us";
        return View(new ContactViewModel());
    }

    [HttpPost("contact")]
    public async Task<IActionResult> Contact(ContactViewModel vm)
    {
        ViewData["Title"] = "Contact Us";
        if (!ModelState.IsValid) return View(vm);
        db.ContactMessages.Add(new ContactMessage { Name = vm.Name, Email = vm.Email, Phone = vm.Phone, Subject = vm.Subject, Message = vm.Message });
        await db.SaveChangesAsync();
        var s = await settings.GetAsync();
        if (!string.IsNullOrWhiteSpace(s.AdminAlertEmail))
            await notify.SendEmailAsync(s.AdminAlertEmail, "New contact message: " + (vm.Subject ?? vm.Name),
                $"<p><b>{WebUtility.HtmlEncode(vm.Name)}</b> ({WebUtility.HtmlEncode(vm.Email)}, {WebUtility.HtmlEncode(vm.Phone)})</p><p>{WebUtility.HtmlEncode(vm.Message).Replace("\n", "<br>")}</p>");
        TempData["Success"] = "Thanks for reaching out! We'll get back to you soon.";
        return RedirectToAction(nameof(Contact));
    }

    [HttpPost("newsletter")]
    public async Task<IActionResult> Newsletter(string email)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || email.Length == 0)
            return Json(new { ok = false, message = "Please enter a valid email address." });
        if (!await db.NewsletterSubscribers.AnyAsync(x => x.Email == email))
        {
            db.NewsletterSubscribers.Add(new NewsletterSubscriber { Email = email });
            await db.SaveChangesAsync();
        }
        return Json(new { ok = true, message = "You're subscribed! Watch your inbox for deals." });
    }
}
