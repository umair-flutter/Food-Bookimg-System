using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using Food_Booking_Application.Infrastructure;
using Food_Booking_Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Food_Booking_Application.Controllers;

public class MenuController(AppDbContext db, SiteContext site) : Controller
{
    private const int PageSize = 12;

    [Route("menu")]
    [Route("search")]
    public Task<IActionResult> Index(string? q, string? category, string? brand, string? tag, string sort = "popular",
        decimal? minPrice = null, decimal? maxPrice = null, bool veg = false, int page = 1)
        => List(q, category, brand, tag, sort, minPrice, maxPrice, veg, page);

    [Route("category/{category}")]
    public Task<IActionResult> Category(string category, string? q, string? brand, string sort = "popular",
        decimal? minPrice = null, decimal? maxPrice = null, bool veg = false, int page = 1)
        => List(q, category, brand, null, sort, minPrice, maxPrice, veg, page);

    [Route("brand/{brand}")]
    public Task<IActionResult> Brand(string brand, string? q, string? category, string sort = "popular",
        decimal? minPrice = null, decimal? maxPrice = null, bool veg = false, int page = 1)
        => List(q, category, brand, null, sort, minPrice, maxPrice, veg, page);

    [Route("tag/{tag}")]
    public Task<IActionResult> Tag(string tag, string? q, string? category, string sort = "popular",
        decimal? minPrice = null, decimal? maxPrice = null, bool veg = false, int page = 1)
        => List(q, category, null, tag, sort, minPrice, maxPrice, veg, page);

    private async Task<IActionResult> List(string? q, string? category, string? brand, string? tag, string sort,
        decimal? minPrice, decimal? maxPrice, bool veg, int page)
    {
        var vm = new MenuViewModel
        {
            Q = q?.Trim(), Category = category, Brand = brand, Tag = tag, Sort = sort, MinPrice = minPrice, MaxPrice = maxPrice, Veg = veg,
            Page = Math.Max(1, page),
            Categories = await site.CategoriesAsync(),
            Brands = await db.Brands.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.SortOrder).ToListAsync(),
            Tags = await db.Tags.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToListAsync(),
            TopBanners = await site.BannersAsync(BannerPosition.MenuPageTop)
        };

        var query = db.Products.AsNoTracking().Include(p => p.Images).Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
            .Where(p => p.IsActive && p.Category!.IsActive);

        if (!string.IsNullOrEmpty(category))
        {
            var cat = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == category && c.IsActive);
            if (cat == null) return NotFound();
            var childIds = await db.Categories.Where(c => c.ParentId == cat.Id).Select(c => c.Id).ToListAsync();
            query = query.Where(p => p.CategoryId == cat.Id || childIds.Contains(p.CategoryId));
            vm.Title = cat.Name; vm.Subtitle = cat.Description; vm.MetaDescription = cat.MetaDescription ?? cat.Description;
            ViewData["Title"] = cat.MetaTitle ?? cat.Name;
        }
        if (!string.IsNullOrEmpty(brand))
        {
            var b = await db.Brands.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == brand && x.IsActive);
            if (b == null) return NotFound();
            query = query.Where(p => p.BrandId == b.Id);
            vm.Title = string.IsNullOrEmpty(category) ? b.Name : $"{vm.Title} by {b.Name}"; vm.Subtitle ??= b.Description;
            ViewData["Title"] ??= b.Name;
        }
        if (!string.IsNullOrEmpty(tag))
        {
            var t = await db.Tags.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == tag && x.IsActive);
            if (t == null) return NotFound();
            query = query.Where(p => p.ProductTags.Any(pt => pt.TagId == t.Id));
            vm.Title = t.Name; vm.Subtitle = t.Subtitle;
            ViewData["Title"] = t.Name;
        }
        if (!string.IsNullOrEmpty(vm.Q))
        {
            var term = vm.Q;
            query = query.Where(p => p.Name.Contains(term) || (p.ShortDescription != null && p.ShortDescription.Contains(term))
                                     || p.Category!.Name.Contains(term) || (p.Brand != null && p.Brand.Name.Contains(term))
                                     || (p.Sku != null && p.Sku == term));
            vm.Title = $"Search results for “{term}”";
            ViewData["Title"] = "Search: " + term;
        }
        if (veg) query = query.Where(p => p.IsVegetarian);
        if (minPrice.HasValue) query = query.Where(p => (p.SalePrice != null && p.SalePrice > 0 && p.SalePrice < p.Price ? p.SalePrice : p.Price) >= minPrice);
        if (maxPrice.HasValue) query = query.Where(p => (p.SalePrice != null && p.SalePrice > 0 && p.SalePrice < p.Price ? p.SalePrice : p.Price) <= maxPrice);

        query = sort switch
        {
            "newest" => query.OrderByDescending(p => p.CreatedAt),
            "price-asc" => query.OrderBy(p => p.SalePrice != null && p.SalePrice > 0 && p.SalePrice < p.Price ? p.SalePrice : p.Price),
            "price-desc" => query.OrderByDescending(p => p.SalePrice != null && p.SalePrice > 0 && p.SalePrice < p.Price ? p.SalePrice : p.Price),
            "name" => query.OrderBy(p => p.Name),
            _ => query.OrderByDescending(p => p.SoldCount).ThenBy(p => p.SortOrder)
        };

        vm.TotalCount = await query.CountAsync();
        vm.TotalPages = (int)Math.Ceiling(vm.TotalCount / (double)PageSize);
        vm.Products = await query.Skip((vm.Page - 1) * PageSize).Take(PageSize).ToListAsync();
        ViewData["Title"] ??= "Menu";
        return View("Index", vm);
    }

    [Route("product/{slug}")]
    public async Task<IActionResult> Product(string slug)
    {
        var p = await db.Products
            .Include(x => x.Images.OrderBy(i => i.SortOrder))
            .Include(x => x.Category).Include(x => x.Brand)
            .Include(x => x.ProductTags).ThenInclude(pt => pt.Tag)
            .Include(x => x.Specifications.OrderBy(s => s.SortOrder))
            .Include(x => x.Options).ThenInclude(o => o.AttributeValue!).ThenInclude(v => v.Attribute)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive);
        if (p == null) return NotFound();

        p.ViewCount++;
        await db.SaveChangesAsync();

        var recentIds = RecentlyViewed.Get(Request).Where(id => id != p.Id).Take(6).ToList();
        RecentlyViewed.Add(HttpContext, p.Id);
        var recent = await db.Products.AsNoTracking().Include(x => x.Images).Where(x => recentIds.Contains(x.Id) && x.IsActive).ToListAsync();

        var vm = new ProductViewModel
        {
            Product = p,
            OptionGroups = p.Options
                .OrderBy(o => o.AttributeValue!.Attribute!.SortOrder).ThenBy(o => o.AttributeValue!.SortOrder)
                .GroupBy(o => o.AttributeValue!.Attribute!).ToList(),
            Related = await db.Products.AsNoTracking().Include(x => x.Images)
                .Where(x => x.IsActive && x.CategoryId == p.CategoryId && x.Id != p.Id)
                .OrderByDescending(x => x.SoldCount).Take(4).ToListAsync(),
            RecentlyViewed = recentIds.Select(id => recent.FirstOrDefault(r => r.Id == id)).Where(r => r != null).ToList()!,
            SideBanners = await site.BannersAsync(BannerPosition.ProductPageSide)
        };
        ViewData["Title"] = p.MetaTitle ?? p.Name;
        ViewData["MetaDescription"] = p.MetaDescription ?? p.ShortDescription;
        ViewData["OgImage"] = p.MainImage;
        return View(vm);
    }

    /// <summary>Autocomplete for the header search box.</summary>
    [Route("search/suggest")]
    public async Task<IActionResult> Suggest(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Json(Array.Empty<object>());
        q = q.Trim();
        var s = await site.SettingsAsync();
        var items = await db.Products.AsNoTracking().Include(p => p.Images)
            .Where(p => p.IsActive && (p.Name.Contains(q) || p.Category!.Name.Contains(q)))
            .OrderByDescending(p => p.SoldCount).Take(6).ToListAsync();
        return Json(items.Select(p => new
        {
            name = p.Name, url = Url.Action(nameof(Product), new { slug = p.Slug }), image = p.MainImage,
            price = s.CurrencySymbol + p.SellingPrice.ToString("N2")
        }));
    }
}
