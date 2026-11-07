using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodBooking.Core.Services;

/// <summary>Shopping cart as stored in the session. Only ids + quantities: prices are always recomputed from the database.</summary>
public class Cart
{
    public List<CartLine> Lines { get; set; } = new();
    public string? CouponCode { get; set; }
    public decimal Tip { get; set; }
    public int Count => Lines.Sum(l => l.Quantity);
}

public class CartLine
{
    public string Key { get; set; } = "";
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public List<int> OptionIds { get; set; } = new();
    public string? Instructions { get; set; }

    /// <summary>Lines with the same product, options and instructions are merged into one.</summary>
    public static string MakeSignature(int productId, IEnumerable<int> optionIds, string? instructions) =>
        productId + ":" + string.Join(",", optionIds.OrderBy(x => x)) + ":" + (instructions ?? "").Trim().ToLowerInvariant();

    public string Signature => MakeSignature(ProductId, OptionIds, Instructions);
}

public class PricedLine
{
    public required CartLine Line { get; init; }
    public required Product Product { get; init; }
    public List<ProductOption> Options { get; init; } = new();
    public decimal UnitPrice { get; init; }
    public decimal LineTotal => UnitPrice * Line.Quantity;
    public string OptionsText => string.Join(", ",
        Options.Select(o => $"{o.AttributeValue!.Attribute!.Name}: {o.AttributeValue.Value}")
            .Concat(string.IsNullOrWhiteSpace(Line.Instructions) ? [] : new[] { "Note: " + Line.Instructions!.Trim() }));
    public string? Problem { get; init; }
}

public class CartSummary
{
    public List<PricedLine> Lines { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public Coupon? Coupon { get; set; }
    public string? CouponMessage { get; set; }
    public decimal Tax { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal Tip { get; set; }
    public decimal Total { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int Count => Lines.Sum(l => l.Line.Quantity);
    public bool HasProblems => Lines.Any(l => l.Problem != null);
    public bool BelowMinimum => MinOrderAmount > 0 && SubTotal < MinOrderAmount;
}

public class PricingService(AppDbContext db, SettingsService settings)
{
    public async Task<CartSummary> PriceAsync(Cart cart, OrderType orderType = OrderType.Delivery)
    {
        var s = await settings.GetAsync();
        var summary = new CartSummary { TaxPercent = s.TaxPercent, MinOrderAmount = s.MinOrderAmount };
        if (cart.Lines.Count == 0) return summary;

        var productIds = cart.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Options).ThenInclude(o => o.AttributeValue!).ThenInclude(v => v.Attribute)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var line in cart.Lines.ToList())
        {
            if (!products.TryGetValue(line.ProductId, out var p)) { cart.Lines.Remove(line); continue; }
            var opts = p.Options.Where(o => line.OptionIds.Contains(o.Id)).ToList();
            string? problem = null;
            if (!p.IsActive) problem = "This item is no longer available.";
            else if (p.TrackStock && p.StockQuantity < line.Quantity)
                problem = p.StockQuantity <= 0 ? "Out of stock." : $"Only {p.StockQuantity} left in stock.";
            summary.Lines.Add(new PricedLine
            {
                Line = line, Product = p, Options = opts,
                UnitPrice = p.SellingPrice + opts.Sum(o => o.PriceAdjustment),
                Problem = problem
            });
        }

        summary.SubTotal = summary.Lines.Sum(l => l.LineTotal);

        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            var (coupon, discount, error) = await ValidateCouponAsync(cart.CouponCode, summary.SubTotal);
            if (coupon != null) { summary.Coupon = coupon; summary.Discount = discount; summary.CouponMessage = $"Coupon {coupon.Code} applied."; }
            else summary.CouponMessage = error;
        }

        var taxable = Math.Max(0, summary.SubTotal - summary.Discount);
        summary.Tax = Math.Round(taxable * s.TaxPercent / 100m, 2);
        summary.DeliveryCharge = orderType == OrderType.Delivery && !(s.FreeDeliveryAbove > 0 && summary.SubTotal >= s.FreeDeliveryAbove)
            ? s.DeliveryCharge : 0;
        summary.Tip = s.EnableTips ? Math.Max(0, Math.Round(cart.Tip, 2)) : 0;
        summary.Total = taxable + summary.Tax + summary.DeliveryCharge + summary.Tip;
        return summary;
    }

    public async Task<(Coupon? coupon, decimal discount, string? error)> ValidateCouponAsync(string code, decimal subTotal)
    {
        code = code.Trim().ToUpperInvariant();
        var c = await db.Coupons.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code);
        var now = DateTime.UtcNow;
        if (c == null || !c.IsActive) return (null, 0, "Invalid coupon code.");
        if (c.StartsAt.HasValue && c.StartsAt > now) return (null, 0, "This coupon is not active yet.");
        if (c.ExpiresAt.HasValue && c.ExpiresAt < now) return (null, 0, "This coupon has expired.");
        if (c.UsageLimit.HasValue && c.UsedCount >= c.UsageLimit) return (null, 0, "This coupon has reached its usage limit.");
        if (subTotal < c.MinOrderAmount) return (null, 0, $"Minimum order of {c.MinOrderAmount:N2} required for this coupon.");
        var discount = c.DiscountType == DiscountType.Percentage ? Math.Round(subTotal * c.Value / 100m, 2) : c.Value;
        if (c.MaxDiscount.HasValue && c.MaxDiscount > 0) discount = Math.Min(discount, c.MaxDiscount.Value);
        discount = Math.Min(discount, subTotal);
        return (c, discount, null);
    }
}
