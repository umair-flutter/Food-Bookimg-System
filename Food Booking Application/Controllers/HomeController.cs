using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using Food_Booking_Application.Infrastructure;
using Food_Booking_Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Food_Booking_Application.Controllers;

public class HomeController(AppDbContext db, SiteContext site) : Controller
{
    [Route("")]
    [Route("home")]
    public async Task<IActionResult> Index()
    {
        var vm = new HomeViewModel
        {
            Slides = await site.BannersAsync(BannerPosition.HomeSlider),
            MiddleBanners = await site.BannersAsync(BannerPosition.HomeMiddle),
            BottomBanners = await site.BannersAsync(BannerPosition.HomeBottom),
            Categories = await db.Categories.AsNoTracking().Where(c => c.IsActive && c.ShowOnHome).OrderBy(c => c.SortOrder).ToListAsync(),
            Brands = await db.Brands.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.SortOrder).ToListAsync()
        };

        var tags = await db.Tags.AsNoTracking().Where(t => t.IsActive && t.ShowOnHome).OrderBy(t => t.SortOrder).ToListAsync();
        foreach (var t in tags)
        {
            var q = db.Products.AsNoTracking().Include(p => p.Images).Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
                .Where(p => p.IsActive && p.ProductTags.Any(pt => pt.TagId == t.Id));
            q = t.Slug == "latest" ? q.OrderByDescending(p => p.CreatedAt) : t.Slug.Contains("best") ? q.OrderByDescending(p => p.SoldCount) : q.OrderBy(p => p.SortOrder);
            var products = await q.Take(Math.Max(1, t.HomeProductCount)).ToListAsync();
            if (products.Count > 0) vm.TagSections.Add((t, products));
        }

        var recent = RecentlyViewed.Get(Request);
        if (recent.Count > 0)
        {
            var list = await db.Products.AsNoTracking().Include(p => p.Images).Where(p => recent.Contains(p.Id) && p.IsActive).ToListAsync();
            vm.RecentlyViewed = recent.Select(id => list.FirstOrDefault(p => p.Id == id)).Where(p => p != null).Take(6).ToList()!;
        }
        return View(vm);
    }

    [Route("home/error")]
    public IActionResult Error() => View("Error");

    [Route("home/status/{code:int}")]
    public IActionResult Status(int code)
    {
        Response.StatusCode = code;
        return code == 404 ? View("NotFound") : View("Error");
    }
}
