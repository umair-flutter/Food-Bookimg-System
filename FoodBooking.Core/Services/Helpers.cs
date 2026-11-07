using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FoodBooking.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace FoodBooking.Core.Services;

public static partial class Slug
{
    public static string Create(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Guid.NewGuid().ToString("N")[..8];
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        var s = sb.ToString().ToLowerInvariant();
        s = NonAlnum().Replace(s, "-").Trim('-');
        s = Dashes().Replace(s, "-");
        return s.Length > 100 ? s[..100].Trim('-') : (s.Length == 0 ? Guid.NewGuid().ToString("N")[..8] : s);
    }

    [GeneratedRegex("[^a-z0-9]+")] private static partial Regex NonAlnum();
    [GeneratedRegex("-{2,}")] private static partial Regex Dashes();
}

/// <summary>PBKDF2 password hashing (ASP.NET Core Identity algorithm).</summary>
public static class Passwords
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object User = new();

    public static string Hash(string password) => Hasher.HashPassword(User, password);

    public static bool Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        try { return Hasher.VerifyHashedPassword(User, hash, password) != PasswordVerificationResult.Failed; }
        catch (FormatException) { return false; }
    }
}

public static class Display
{
    public static string StatusText(this OrderStatus s) => s switch
    {
        OrderStatus.Pending => "Pending",
        OrderStatus.Confirmed => "Confirmed",
        OrderStatus.Preparing => "Preparing",
        OrderStatus.ReadyForPickup => "Ready for Pickup",
        OrderStatus.OutForDelivery => "Out for Delivery",
        OrderStatus.Delivered => "Delivered",
        OrderStatus.Cancelled => "Cancelled",
        _ => s.ToString()
    };

    public static string StatusBadge(this OrderStatus s) => s switch
    {
        OrderStatus.Pending => "warning",
        OrderStatus.Confirmed => "info",
        OrderStatus.Preparing => "primary",
        OrderStatus.ReadyForPickup or OrderStatus.OutForDelivery => "secondary",
        OrderStatus.Delivered => "success",
        OrderStatus.Cancelled => "danger",
        _ => "light"
    };

    public static string PaymentText(this PaymentMethod m) => m switch
    {
        PaymentMethod.CashOnDelivery => "Cash on Delivery",
        PaymentMethod.CreditCard => "Credit / Debit Card",
        PaymentMethod.PayPal => "PayPal",
        _ => m.ToString()
    };

    public static string PaymentBadge(this PaymentStatus s) => s switch
    {
        PaymentStatus.Paid => "success",
        PaymentStatus.Failed => "danger",
        PaymentStatus.Refunded => "secondary",
        _ => "warning"
    };

    public static string Money(this decimal value, string symbol) =>
        (value < 0 ? "-" : "") + symbol + Math.Abs(value).ToString("N2", CultureInfo.InvariantCulture);

    public static string PositionText(this BannerPosition p) => p switch
    {
        BannerPosition.HomeSlider => "Home - Main Slider",
        BannerPosition.HomeMiddle => "Home - Middle Banner",
        BannerPosition.HomeBottom => "Home - Bottom Banner",
        BannerPosition.HeaderTop => "Header - Top Strip",
        BannerPosition.MenuPageTop => "Menu Page - Top",
        BannerPosition.ProductPageSide => "Product Page - Side",
        BannerPosition.CartPage => "Cart Page",
        _ => p.ToString()
    };

    /// <summary>All statuses an order can move to from the given one (used by admin + kitchen).</summary>
    public static IEnumerable<OrderStatus> NextStatuses(this Order o)
    {
        if (o.Status is OrderStatus.Delivered or OrderStatus.Cancelled) yield break;
        foreach (var s in Enum.GetValues<OrderStatus>())
        {
            if (s <= o.Status) continue;
            if (o.OrderType == OrderType.Pickup && s == OrderStatus.OutForDelivery) continue;
            if (o.OrderType == OrderType.Delivery && s == OrderStatus.ReadyForPickup) continue;
            yield return s;
        }
    }
}
