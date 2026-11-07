using System.Security.Claims;
using System.Text.Json;
using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Food_Booking_Application.Infrastructure;

/// <summary>Session-backed shopping cart.</summary>
public class CartStore(IHttpContextAccessor http)
{
    private const string Key = "cart";
    private ISession Session => http.HttpContext!.Session;

    public Cart Get()
    {
        var json = Session.GetString(Key);
        if (string.IsNullOrEmpty(json)) return new Cart();
        try { return JsonSerializer.Deserialize<Cart>(json) ?? new Cart(); } catch { return new Cart(); }
    }

    public void Save(Cart cart) => Session.SetString(Key, JsonSerializer.Serialize(cart));
    public void Clear() => Session.Remove(Key);
}

/// <summary>Shared data for the layout (settings, menu, footer pages, store status). Loaded once per request.</summary>
public class SiteContext(AppDbContext db, SettingsService settings, StoreHoursService hours, CartStore cart)
{
    private SiteSetting? _settings;
    private List<Category>? _categories;
    private List<CmsPage>? _pages;
    private StoreStatus? _status;

    public async Task<SiteSetting> SettingsAsync() => _settings ??= await settings.GetAsync();

    public async Task<List<Category>> CategoriesAsync() => _categories ??= await db.Categories.AsNoTracking()
        .Where(c => c.IsActive && c.ParentId == null)
        .Include(c => c.Children.Where(ch => ch.IsActive))
        .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();

    public async Task<List<CmsPage>> PagesAsync() => _pages ??= await db.CmsPages.AsNoTracking()
        .Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();

    public async Task<StoreStatus> StatusAsync() => _status ??= await hours.GetStatusAsync();

    public async Task<List<Banner>> BannersAsync(BannerPosition position)
    {
        var now = DateTime.UtcNow;
        return await db.Banners.AsNoTracking()
            .Where(b => b.IsActive && b.Position == position && (b.StartDate == null || b.StartDate <= now) && (b.EndDate == null || b.EndDate >= now))
            .OrderBy(b => b.SortOrder).ToListAsync();
    }

    public async Task<List<BusinessHour>> HoursAsync() =>
        await db.BusinessHours.AsNoTracking().OrderBy(h => h.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)h.DayOfWeek).ToListAsync();

    public int CartCount => cart.Get().Count;

    public async Task<string> MoneyAsync(decimal v) => v.Money((await SettingsAsync()).CurrencySymbol);
}

public static class RecentlyViewed
{
    private const string Cookie = "recent";
    public static List<int> Get(HttpRequest req) =>
        (req.Cookies[Cookie] ?? "").Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var i) ? i : 0).Where(i => i > 0).ToList();

    public static void Add(HttpContext ctx, int productId)
    {
        var list = Get(ctx.Request);
        list.Remove(productId);
        list.Insert(0, productId);
        ctx.Response.Cookies.Append(Cookie, string.Join("-", list.Take(12)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(60), HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax });
    }
}

public static class UserExtensions
{
    public static int? CustomerId(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
