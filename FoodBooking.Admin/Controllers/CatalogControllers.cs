using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FoodBooking.Admin.Controllers;

public class CategoriesController(AppDbContext db, FileStorage storage) : AdminController
{
    public async Task<IActionResult> Index() =>
        View(await db.Categories.AsNoTracking().Include(c => c.Parent).Include(c => c.Products)
            .OrderBy(c => c.ParentId == null ? c.SortOrder : c.Parent!.SortOrder).ThenBy(c => c.ParentId).ThenBy(c => c.SortOrder).ToListAsync());

    public async Task<IActionResult> Create() { await Lists(null); return View("Form", new Category()); }

    public async Task<IActionResult> Edit(int id)
    {
        var c = await db.Categories.FindAsync(id);
        if (c == null) return NotFound();
        await Lists(c.Id);
        return View("Form", c);
    }

    [HttpPost]
    public async Task<IActionResult> Save(Category m, IFormFile? image)
    {
        ModelState.Remove(nameof(Category.Slug));
        m.Slug = Slug.Create(string.IsNullOrWhiteSpace(m.Slug) ? m.Name : m.Slug);
        if (await db.Categories.AnyAsync(c => c.Slug == m.Slug && c.Id != m.Id)) ModelState.AddModelError(nameof(m.Slug), "Slug already in use.");
        if (m.ParentId == m.Id && m.Id != 0) ModelState.AddModelError(nameof(m.ParentId), "A category cannot be its own parent.");
        var url = await TryUpload(storage, image, "categories", "image");
        if (!ModelState.IsValid) { await Lists(m.Id); return View("Form", m); }

        var c = m.Id == 0 ? new Category() : await db.Categories.FindAsync(m.Id);
        if (c == null) return NotFound();
        c.Name = m.Name; c.Slug = m.Slug; c.ParentId = m.ParentId; c.Description = m.Description; c.Icon = m.Icon;
        c.SortOrder = m.SortOrder; c.ShowOnHome = m.ShowOnHome; c.IsActive = m.IsActive; c.MetaTitle = m.MetaTitle; c.MetaDescription = m.MetaDescription;
        if (url != null) { storage.Delete(c.ImageUrl); c.ImageUrl = url; }
        if (m.Id == 0) db.Categories.Add(c);
        await db.SaveChangesAsync();
        Success($"Category \"{c.Name}\" saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Categories.Include(x => x.Products).Include(x => x.Children).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        if (c.Products.Count > 0 || c.Children.Count > 0) { Error("Move or delete this category's products and sub-categories first."); return RedirectToAction(nameof(Index)); }
        db.Categories.Remove(c);
        await db.SaveChangesAsync();
        storage.Delete(c.ImageUrl);
        Success("Category deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task Lists(int? currentId) =>
        ViewBag.Parents = new SelectList(await db.Categories.Where(c => c.ParentId == null && c.Id != currentId).OrderBy(c => c.Name).ToListAsync(), "Id", "Name");
}

public class BrandsController(AppDbContext db, FileStorage storage) : AdminController
{
    public async Task<IActionResult> Index() => View(await db.Brands.AsNoTracking().Include(b => b.Products).OrderBy(b => b.SortOrder).ThenBy(b => b.Name).ToListAsync());
    public IActionResult Create() => View("Form", new Brand());

    public async Task<IActionResult> Edit(int id) => await db.Brands.FindAsync(id) is { } b ? View("Form", b) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Save(Brand m, IFormFile? logo)
    {
        ModelState.Remove(nameof(Brand.Slug));
        m.Slug = Slug.Create(string.IsNullOrWhiteSpace(m.Slug) ? m.Name : m.Slug);
        if (await db.Brands.AnyAsync(c => c.Slug == m.Slug && c.Id != m.Id)) ModelState.AddModelError(nameof(m.Slug), "Slug already in use.");
        var url = await TryUpload(storage, logo, "brands", "logo");
        if (!ModelState.IsValid) return View("Form", m);
        var b = m.Id == 0 ? new Brand() : await db.Brands.FindAsync(m.Id);
        if (b == null) return NotFound();
        b.Name = m.Name; b.Slug = m.Slug; b.Description = m.Description; b.SortOrder = m.SortOrder; b.IsActive = m.IsActive;
        if (url != null) { storage.Delete(b.LogoUrl); b.LogoUrl = url; }
        if (m.Id == 0) db.Brands.Add(b);
        await db.SaveChangesAsync();
        Success($"Brand \"{b.Name}\" saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var b = await db.Brands.FindAsync(id);
        if (b == null) return NotFound();
        db.Brands.Remove(b);
        await db.SaveChangesAsync();
        storage.Delete(b.LogoUrl);
        Success("Brand deleted. Its products are now unbranded.");
        return RedirectToAction(nameof(Index));
    }
}

public class TagsController(AppDbContext db) : AdminController
{
    public async Task<IActionResult> Index() => View(await db.Tags.AsNoTracking().Include(t => t.ProductTags).OrderBy(t => t.SortOrder).ToListAsync());
    public IActionResult Create() => View("Form", new Tag());
    public async Task<IActionResult> Edit(int id) => await db.Tags.FindAsync(id) is { } t ? View("Form", t) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Save(Tag m)
    {
        ModelState.Remove(nameof(Tag.Slug));
        m.Slug = Slug.Create(string.IsNullOrWhiteSpace(m.Slug) ? m.Name : m.Slug);
        if (await db.Tags.AnyAsync(c => c.Slug == m.Slug && c.Id != m.Id)) ModelState.AddModelError(nameof(m.Slug), "Slug already in use.");
        if (!ModelState.IsValid) return View("Form", m);
        var t = m.Id == 0 ? new Tag() : await db.Tags.FindAsync(m.Id);
        if (t == null) return NotFound();
        t.Name = m.Name; t.Slug = m.Slug; t.Subtitle = m.Subtitle; t.BadgeColor = m.BadgeColor; t.ShowOnHome = m.ShowOnHome;
        t.HomeProductCount = Math.Clamp(m.HomeProductCount, 1, 24); t.SortOrder = m.SortOrder; t.IsActive = m.IsActive;
        if (m.Id == 0) db.Tags.Add(t);
        await db.SaveChangesAsync();
        Success($"Tag \"{t.Name}\" saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await db.Tags.Include(x => x.ProductTags).FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        db.ProductTags.RemoveRange(t.ProductTags);
        db.Tags.Remove(t);
        await db.SaveChangesAsync();
        Success("Tag deleted.");
        return RedirectToAction(nameof(Index));
    }
}

public class AttributesController(AppDbContext db) : AdminController
{
    public async Task<IActionResult> Index() =>
        View(await db.ProductAttributes.AsNoTracking().Include(a => a.Values.OrderBy(v => v.SortOrder)).OrderBy(a => a.SortOrder).ToListAsync());

    public IActionResult Create() => View("Form", new ProductAttribute());

    public async Task<IActionResult> Edit(int id) =>
        await db.ProductAttributes.Include(a => a.Values.OrderBy(v => v.SortOrder)).FirstOrDefaultAsync(a => a.Id == id) is { } a ? View("Form", a) : NotFound();

    /// <summary>Saves the attribute and its values. Values are posted as parallel arrays (valueIds/valueNames).</summary>
    [HttpPost]
    public async Task<IActionResult> Save(ProductAttribute m, List<int> valueIds, List<string?> valueNames)
    {
        ModelState.Remove(nameof(ProductAttribute.Values));
        if (!ModelState.IsValid) return View("Form", m);
        var a = m.Id == 0 ? new ProductAttribute() : await db.ProductAttributes.Include(x => x.Values).FirstOrDefaultAsync(x => x.Id == m.Id);
        if (a == null) return NotFound();
        a.Name = m.Name; a.InputType = m.InputType; a.IsRequired = m.IsRequired && m.InputType == AttributeInputType.SingleSelect; a.SortOrder = m.SortOrder;

        var keep = new HashSet<int>();
        for (int i = 0; i < valueNames.Count; i++)
        {
            var name = valueNames[i]?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            var vid = i < valueIds.Count ? valueIds[i] : 0;
            var v = vid > 0 ? a.Values.FirstOrDefault(x => x.Id == vid) : null;
            if (v == null) a.Values.Add(v = new AttributeValue());
            v.Value = name; v.SortOrder = i;
            if (v.Id > 0) keep.Add(v.Id);
        }
        // Removing a value also removes it from every product using it (cascade)
        db.AttributeValues.RemoveRange(a.Values.Where(v => v.Id > 0 && !keep.Contains(v.Id)));
        if (m.Id == 0) db.ProductAttributes.Add(a);
        await db.SaveChangesAsync();
        Success($"Attribute \"{a.Name}\" saved. Assign its values to products in the product editor.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await db.ProductAttributes.Include(x => x.Values).FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        db.AttributeValues.RemoveRange(a.Values);
        db.ProductAttributes.Remove(a);
        await db.SaveChangesAsync();
        Success("Attribute deleted.");
        return RedirectToAction(nameof(Index));
    }
}
