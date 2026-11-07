using FoodBooking.Admin.Models;
using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FoodBooking.Admin.Controllers;

public class ProductsController(AppDbContext db, FileStorage storage) : AdminController
{
    public async Task<IActionResult> Index(string? q, int? categoryId, int? brandId, string? status, int page = 1)
    {
        var query = db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Brand).Include(p => p.Images).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => p.Name.Contains(q) || (p.Sku != null && p.Sku.Contains(q)));
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId);
        if (brandId.HasValue) query = query.Where(p => p.BrandId == brandId);
        if (status == "active") query = query.Where(p => p.IsActive);
        else if (status == "inactive") query = query.Where(p => !p.IsActive);
        else if (status == "lowstock") query = query.Where(p => p.TrackStock && p.StockQuantity <= 5);
        var list = new PagedList<Product> { Page = Math.Max(1, page), Total = await query.CountAsync() };
        list.Items = await query.OrderBy(p => p.Category!.SortOrder).ThenBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Skip((list.Page - 1) * list.PageSize).Take(list.PageSize).ToListAsync();
        ViewBag.Categories = new SelectList(await db.Categories.OrderBy(c => c.Name).ToListAsync(), "Id", "Name", categoryId);
        ViewBag.Brands = new SelectList(await db.Brands.OrderBy(c => c.Name).ToListAsync(), "Id", "Name", brandId);
        ViewBag.Q = q; ViewBag.Status = status;
        return View(list);
    }

    public async Task<IActionResult> Create()
    {
        var f = new ProductForm();
        await PrepareForm(f, null);
        return View("Form", f);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var p = await db.Products.Include(x => x.Images).Include(x => x.ProductTags).Include(x => x.Options).Include(x => x.Specifications)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        var f = new ProductForm
        {
            Id = p.Id, Name = p.Name, Slug = p.Slug, Sku = p.Sku, ShortDescription = p.ShortDescription, Description = p.Description,
            Price = p.Price, SalePrice = p.SalePrice, CategoryId = p.CategoryId, BrandId = p.BrandId, TrackStock = p.TrackStock,
            StockQuantity = p.StockQuantity, IsVegetarian = p.IsVegetarian, IsSpicy = p.IsSpicy, PrepTimeMinutes = p.PrepTimeMinutes,
            IsActive = p.IsActive, SortOrder = p.SortOrder, MetaTitle = p.MetaTitle, MetaDescription = p.MetaDescription, CloverItemId = p.CloverItemId,
            TagIds = p.ProductTags.Select(t => t.TagId).ToList(),
            Specs = p.Specifications.OrderBy(s => s.SortOrder).Select(s => new SpecRow { Name = s.Name, Value = s.Value }).ToList(),
            Images = p.Images.OrderBy(i => i.SortOrder).ToList()
        };
        await PrepareForm(f, p);
        return View("Form", f);
    }

    [HttpPost]
    public async Task<IActionResult> Save(ProductForm f, List<IFormFile>? newImages)
    {
        if (f.SalePrice.HasValue && f.SalePrice >= f.Price) ModelState.AddModelError(nameof(f.SalePrice), "Sale price must be lower than the regular price.");
        var slug = Slug.Create(string.IsNullOrWhiteSpace(f.Slug) ? f.Name : f.Slug);
        if (await db.Products.AnyAsync(p => p.Slug == slug && p.Id != f.Id)) ModelState.AddModelError(nameof(f.Slug), "Another product already uses this URL slug.");

        Product? p = f.Id == 0 ? new Product() : await db.Products.Include(x => x.Images).Include(x => x.ProductTags)
            .Include(x => x.Options).Include(x => x.Specifications).FirstOrDefaultAsync(x => x.Id == f.Id);
        if (p == null) return NotFound();

        var uploaded = new List<string>();
        foreach (var file in newImages ?? new())
        {
            var url = await TryUpload(storage, file, "products", "newImages");
            if (url != null) uploaded.Add(url);
        }
        if (!ModelState.IsValid)
        {
            foreach (var u in uploaded) storage.Delete(u);
            f.Images = p.Images.OrderBy(i => i.SortOrder).ToList();
            await PrepareForm(f, p, keepPostedOptions: true);
            return View("Form", f);
        }

        p.Name = f.Name.Trim(); p.Slug = slug; p.Sku = f.Sku; p.ShortDescription = f.ShortDescription; p.Description = f.Description;
        p.Price = f.Price; p.SalePrice = f.SalePrice is > 0 ? f.SalePrice : null; p.CategoryId = f.CategoryId; p.BrandId = f.BrandId;
        p.TrackStock = f.TrackStock; p.StockQuantity = f.StockQuantity; p.IsVegetarian = f.IsVegetarian; p.IsSpicy = f.IsSpicy;
        p.PrepTimeMinutes = f.PrepTimeMinutes; p.IsActive = f.IsActive; p.SortOrder = f.SortOrder;
        p.MetaTitle = f.MetaTitle; p.MetaDescription = f.MetaDescription; p.CloverItemId = f.CloverItemId;
        p.UpdatedAt = DateTime.UtcNow;

        // Tags
        p.ProductTags.RemoveAll(t => !f.TagIds.Contains(t.TagId));
        foreach (var tid in f.TagIds.Where(t => p.ProductTags.All(x => x.TagId != t))) p.ProductTags.Add(new ProductTag { TagId = tid });

        // Options (attribute values offered on this product)
        var enabled = f.Options.Where(o => o.Enabled).ToList();
        db.ProductOptions.RemoveRange(p.Options.Where(o => enabled.All(e => e.AttributeValueId != o.AttributeValueId)));
        foreach (var e in enabled)
        {
            var existing = p.Options.FirstOrDefault(o => o.AttributeValueId == e.AttributeValueId);
            if (existing == null) p.Options.Add(existing = new ProductOption { AttributeValueId = e.AttributeValueId });
            existing.PriceAdjustment = e.PriceAdjustment;
            existing.IsDefault = e.IsDefault;
        }

        // Specifications
        db.ProductSpecifications.RemoveRange(p.Specifications);
        p.Specifications.Clear();
        var i = 0;
        foreach (var s in f.Specs.Where(s => !string.IsNullOrWhiteSpace(s.Name) && !string.IsNullOrWhiteSpace(s.Value)))
            p.Specifications.Add(new ProductSpecification { Name = s.Name!.Trim(), Value = s.Value!.Trim(), SortOrder = i++ });

        // Images
        var nextSort = p.Images.Count == 0 ? 0 : p.Images.Max(x => x.SortOrder) + 1;
        foreach (var u in uploaded) p.Images.Add(new ProductImage { Url = u, SortOrder = nextSort++ });

        if (f.Id == 0) db.Products.Add(p);
        await db.SaveChangesAsync();
        Success($"Product \"{p.Name}\" saved.");
        return RedirectToAction(nameof(Edit), new { id = p.Id });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var img = await db.ProductImages.FindAsync(id);
        if (img == null) return NotFound();
        db.ProductImages.Remove(img);
        await db.SaveChangesAsync();
        storage.Delete(img.Url);
        Success("Image removed.");
        return RedirectToAction(nameof(Edit), new { id = img.ProductId });
    }

    /// <summary>Makes an image the main (first) image.</summary>
    [HttpPost]
    public async Task<IActionResult> MainImage(int id)
    {
        var img = await db.ProductImages.FindAsync(id);
        if (img == null) return NotFound();
        var all = await db.ProductImages.Where(x => x.ProductId == img.ProductId).OrderBy(x => x.SortOrder).ToListAsync();
        all.Remove(img); all.Insert(0, img);
        for (int i = 0; i < all.Count; i++) all[i].SortOrder = i;
        await db.SaveChangesAsync();
        Success("Main image updated.");
        return RedirectToAction(nameof(Edit), new { id = img.ProductId });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var p = await db.Products.FindAsync(id);
        if (p == null) return NotFound();
        p.IsActive = !p.IsActive;
        await db.SaveChangesAsync();
        Success($"\"{p.Name}\" is now {(p.IsActive ? "visible" : "hidden")} on the website.");
        return Redirect(Request.Headers.Referer.FirstOrDefault() ?? "/products");
    }

    [HttpPost]
    public async Task<IActionResult> Duplicate(int id)
    {
        var p = await db.Products.AsNoTracking().Include(x => x.Images).Include(x => x.ProductTags).Include(x => x.Options).Include(x => x.Specifications)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        var copy = new Product
        {
            Name = p.Name + " (copy)", Slug = Slug.Create(p.Name + "-copy-" + Guid.NewGuid().ToString("N")[..4]), Sku = null,
            ShortDescription = p.ShortDescription, Description = p.Description, Price = p.Price, SalePrice = p.SalePrice,
            CategoryId = p.CategoryId, BrandId = p.BrandId, TrackStock = p.TrackStock, StockQuantity = p.StockQuantity,
            IsVegetarian = p.IsVegetarian, IsSpicy = p.IsSpicy, PrepTimeMinutes = p.PrepTimeMinutes, IsActive = false, SortOrder = p.SortOrder,
            Images = p.Images.Select(i => new ProductImage { Url = i.Url, SortOrder = i.SortOrder }).ToList(),
            ProductTags = p.ProductTags.Select(t => new ProductTag { TagId = t.TagId }).ToList(),
            Options = p.Options.Select(o => new ProductOption { AttributeValueId = o.AttributeValueId, PriceAdjustment = o.PriceAdjustment, IsDefault = o.IsDefault }).ToList(),
            Specifications = p.Specifications.Select(s => new ProductSpecification { Name = s.Name, Value = s.Value, SortOrder = s.SortOrder }).ToList()
        };
        db.Products.Add(copy);
        await db.SaveChangesAsync();
        Success("Product duplicated (hidden until you activate it).");
        return RedirectToAction(nameof(Edit), new { id = copy.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.Products.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        db.Products.Remove(p);
        await db.SaveChangesAsync();
        // only delete files that no other product (e.g. a duplicate) still uses
        foreach (var img in p.Images)
            if (!await db.ProductImages.AnyAsync(x => x.Url == img.Url)) storage.Delete(img.Url);
        Success($"Product \"{p.Name}\" deleted. Past orders keep their item details.");
        return RedirectToAction(nameof(Index));
    }

    private async Task PrepareForm(ProductForm f, Product? p, bool keepPostedOptions = false)
    {
        ViewBag.Categories = new SelectList(await db.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(), "Id", "Name");
        ViewBag.Brands = new SelectList(await db.Brands.OrderBy(c => c.Name).ToListAsync(), "Id", "Name");
        ViewBag.Tags = await db.Tags.OrderBy(t => t.SortOrder).ToListAsync();
        var attrs = await db.ProductAttributes.Include(a => a.Values.OrderBy(v => v.SortOrder)).OrderBy(a => a.SortOrder).ToListAsync();
        ViewBag.Attributes = attrs;
        if (!keepPostedOptions)
        {
            f.Options = attrs.SelectMany(a => a.Values).Select(v =>
            {
                var existing = p?.Options.FirstOrDefault(o => o.AttributeValueId == v.Id);
                return new OptionRow { AttributeValueId = v.Id, Enabled = existing != null, PriceAdjustment = existing?.PriceAdjustment ?? 0, IsDefault = existing?.IsDefault ?? false };
            }).ToList();
        }
        if (f.Specs.Count == 0) f.Specs.Add(new SpecRow());
    }
}
