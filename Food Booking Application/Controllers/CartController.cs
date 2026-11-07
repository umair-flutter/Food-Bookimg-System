using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;
using Food_Booking_Application.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Food_Booking_Application.Controllers;

[Route("cart")]
public class CartController(AppDbContext db, CartStore store, PricingService pricing, SiteContext site) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var cart = store.Get();
        var summary = await pricing.PriceAsync(cart);
        store.Save(cart);
        ViewData["Title"] = "Your Cart";
        ViewBag.Banners = await site.BannersAsync(BannerPosition.CartPage);
        return View(summary);
    }

    /// <summary>Offcanvas mini cart (loaded via AJAX).</summary>
    [HttpGet("mini")]
    public async Task<IActionResult> Mini()
    {
        var summary = await pricing.PriceAsync(store.Get());
        return PartialView("_MiniCart", summary);
    }

    [HttpPost("add")]
    public async Task<IActionResult> Add(int productId, int quantity = 1, List<int>? optionIds = null, string? instructions = null)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Options).ThenInclude(o => o.AttributeValue!).ThenInclude(v => v.Attribute)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        if (product == null) return Result(false, "This item is not available.");

        quantity = Math.Clamp(quantity, 1, 50);
        optionIds ??= new();
        var valid = product.Options.Where(o => optionIds.Contains(o.Id)).ToList();

        // Required single-select attributes: auto-pick the default (quick-add from grid) or reject
        // Group by attribute id (no-tracking queries give each option its own Attribute instance)
        foreach (var group in product.Options.GroupBy(o => o.AttributeValue!.AttributeId))
        {
            var attr = group.First().AttributeValue!.Attribute!;
            var chosen = valid.Where(v => v.AttributeValue!.AttributeId == group.Key).ToList();
            if (attr.InputType == AttributeInputType.SingleSelect && chosen.Count > 1)
                valid.RemoveAll(v => v.AttributeValue!.AttributeId == group.Key && v != chosen[0]);
            if (attr.IsRequired && chosen.Count == 0)
            {
                var def = group.FirstOrDefault(o => o.IsDefault);
                if (def == null) return Result(false, $"Please choose {attr.Name}.", Url.Action("Product", "Menu", new { slug = product.Slug }));
                valid.Add(def);
            }
        }

        var cart = store.Get();
        var ids = valid.Select(v => v.Id).OrderBy(x => x).ToList();
        instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim()[..Math.Min(200, instructions.Trim().Length)];
        var sig = CartLine.MakeSignature(productId, ids, instructions);
        var existing = cart.Lines.FirstOrDefault(l => l.Signature == sig);
        var newQty = (existing?.Quantity ?? 0) + quantity;
        if (product.TrackStock && newQty > product.StockQuantity)
            return Result(false, product.StockQuantity <= 0 ? "Sorry, this item is out of stock." : $"Only {product.StockQuantity} left in stock.");

        if (existing != null) existing.Quantity = newQty;
        else cart.Lines.Add(new CartLine { Key = Guid.NewGuid().ToString("N")[..10], ProductId = productId, Quantity = quantity, OptionIds = ids, Instructions = instructions });
        store.Save(cart);
        return Result(true, $"{product.Name} added to your cart.");
    }

    [HttpPost("update")]
    public async Task<IActionResult> Update(string key, int quantity)
    {
        var cart = store.Get();
        var line = cart.Lines.FirstOrDefault(l => l.Key == key);
        if (line != null)
        {
            if (quantity <= 0) cart.Lines.Remove(line);
            else
            {
                var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == line.ProductId);
                var max = p is { TrackStock: true } ? Math.Max(0, p.StockQuantity) : 50;
                line.Quantity = Math.Min(quantity, Math.Max(1, max));
            }
            store.Save(cart);
        }
        return await Totals();
    }

    [HttpPost("remove")]
    public async Task<IActionResult> Remove(string key)
    {
        var cart = store.Get();
        cart.Lines.RemoveAll(l => l.Key == key);
        store.Save(cart);
        return await Totals();
    }

    [HttpPost("coupon")]
    public async Task<IActionResult> ApplyCoupon(string? code)
    {
        var cart = store.Get();
        if (string.IsNullOrWhiteSpace(code)) { cart.CouponCode = null; store.Save(cart); return await Totals(); }
        var sub = (await pricing.PriceAsync(cart)).SubTotal;
        var (coupon, _, error) = await pricing.ValidateCouponAsync(code, sub);
        if (coupon == null) return Json(new { ok = false, message = error });
        cart.CouponCode = coupon.Code;
        store.Save(cart);
        return await Totals($"Coupon {coupon.Code} applied!");
    }

    [HttpPost("tip")]
    public async Task<IActionResult> Tip(decimal tip)
    {
        var cart = store.Get();
        cart.Tip = Math.Clamp(tip, 0, 500);
        store.Save(cart);
        return await Totals();
    }

    [HttpPost("clear")]
    public IActionResult Clear()
    {
        store.Clear();
        return RedirectToAction(nameof(Index));
    }

    private IActionResult Result(bool ok, string message, string? redirect = null)
    {
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return Json(new { ok, message, redirect, count = store.Get().Count });
        TempData[ok ? "Success" : "Error"] = message;
        return redirect != null ? Redirect(redirect) : RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> Totals(string? message = null)
    {
        var cart = store.Get();
        var s = await pricing.PriceAsync(cart);
        var st = await site.SettingsAsync();
        string M(decimal v) => v.Money(st.CurrencySymbol);
        if (Request.Headers.XRequestedWith != "XMLHttpRequest") return RedirectToAction(nameof(Index));
        return Json(new
        {
            ok = true, message, count = s.Count,
            subTotal = M(s.SubTotal), discount = M(s.Discount), tax = M(s.Tax), delivery = M(s.DeliveryCharge), tip = M(s.Tip), total = M(s.Total),
            hasDiscount = s.Discount > 0, coupon = s.Coupon?.Code, couponMessage = s.CouponMessage,
            lines = s.Lines.ToDictionary(l => l.Line.Key, l => M(l.LineTotal)),
            belowMinimum = s.BelowMinimum, empty = s.Lines.Count == 0
        });
    }
}
