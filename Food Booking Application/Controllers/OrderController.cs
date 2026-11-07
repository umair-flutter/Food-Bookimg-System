using FoodBooking.Core.Data;
using Food_Booking_Application.Infrastructure;
using Food_Booking_Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Food_Booking_Application.Controllers;

[Route("order")]
public class OrderController(AppDbContext db) : Controller
{
    /// <summary>Printable invoice. Accessible with the secret order key (guests) or by the owning customer.</summary>
    [HttpGet("invoice")]
    public async Task<IActionResult> Invoice(string number, string? key, bool placed = false)
    {
        var o = await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.History)
            .FirstOrDefaultAsync(x => x.OrderNumber == number);
        if (o == null || !(o.AccessKey == key || (o.CustomerId != null && o.CustomerId == User.CustomerId()))) return NotFound();
        ViewBag.Placed = placed;
        ViewData["Title"] = "Invoice #" + o.OrderNumber;
        return View(o);
    }

    [HttpGet("track")]
    public async Task<IActionResult> Track(string? number, string? email, string? key)
    {
        ViewData["Title"] = "Track Your Order";
        var vm = new TrackViewModel { Number = number, Email = email, Key = key };
        if (string.IsNullOrWhiteSpace(number)) return View(vm);

        var o = await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.History)
            .FirstOrDefaultAsync(x => x.OrderNumber == number.Trim());
        var allowed = o != null && ((key != null && o.AccessKey == key)
                                    || (!string.IsNullOrWhiteSpace(email) && string.Equals(o.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))
                                    || (o.CustomerId != null && o.CustomerId == User.CustomerId()));
        if (!allowed) ModelState.AddModelError("", "We couldn't find an order with that number and email address.");
        else vm.Order = o;
        return View(vm);
    }

    /// <summary>Live status polling for the tracking page.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(string number, string key)
    {
        var o = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.OrderNumber == number && x.AccessKey == key);
        return o == null ? NotFound() : Json(new { status = (int)o.Status });
    }
}
