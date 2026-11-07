using System.ComponentModel.DataAnnotations;
using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;

namespace Food_Booking_Application.Models;

public class HomeViewModel
{
    public List<Banner> Slides { get; set; } = new();
    public List<Banner> MiddleBanners { get; set; } = new();
    public List<Banner> BottomBanners { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<(Tag tag, List<Product> products)> TagSections { get; set; } = new();
    public List<Brand> Brands { get; set; } = new();
    public List<Product> RecentlyViewed { get; set; } = new();
}

public class MenuViewModel
{
    public string Title { get; set; } = "Our Menu";
    public string? Subtitle { get; set; }
    public string? Q { get; set; }
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public string? Tag { get; set; }
    public string Sort { get; set; } = "popular";
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool Veg { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public List<Product> Products { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<Brand> Brands { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
    public List<Banner> TopBanners { get; set; } = new();
    public string? MetaDescription { get; set; }

    public Dictionary<string, string?> RouteValues(int? page = null) => new()
    {
        ["q"] = Q, ["category"] = Category, ["brand"] = Brand, ["tag"] = Tag, ["sort"] = Sort,
        ["minPrice"] = MinPrice?.ToString(), ["maxPrice"] = MaxPrice?.ToString(), ["veg"] = Veg ? "true" : null,
        ["page"] = (page ?? Page).ToString()
    };
}

public class ProductViewModel
{
    public required Product Product { get; set; }
    public List<IGrouping<ProductAttribute, ProductOption>> OptionGroups { get; set; } = new();
    public List<Product> Related { get; set; } = new();
    public List<Product> RecentlyViewed { get; set; } = new();
    public List<Banner> SideBanners { get; set; } = new();
}

public class CheckoutViewModel
{
    public CheckoutInput Input { get; set; } = new();
    public required CartSummary Summary { get; set; }
    public required SiteSetting Settings { get; set; }
    public List<DateTime> Slots { get; set; } = new();
    public bool IsOpenNow { get; set; }
    public bool LoggedIn { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool CardGatewayLive { get; set; }
    public bool PaypalLive { get; set; }
}

public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    [Display(Name = "Remember me")] public bool RememberMe { get; set; } = true;
    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, StringLength(60), Display(Name = "First name")] public string FirstName { get; set; } = "";
    [StringLength(60), Display(Name = "Last name")] public string? LastName { get; set; }
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Required, Phone, StringLength(30)] public string Phone { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match."), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
}

public class ResetPasswordViewModel
{
    [Required] public string Token { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password), Display(Name = "New password")] public string Password { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match."), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password), Display(Name = "New password")] public string NewPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match."), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = "";
}

public class ProfileViewModel
{
    [Required, StringLength(60), Display(Name = "First name")] public string FirstName { get; set; } = "";
    [StringLength(60), Display(Name = "Last name")] public string? LastName { get; set; }
    [Required, Phone, StringLength(30)] public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(80)] public string? City { get; set; }
    [StringLength(20), Display(Name = "Postal code")] public string? PostalCode { get; set; }
}

public class ContactViewModel
{
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Phone, StringLength(30)] public string? Phone { get; set; }
    [StringLength(200)] public string? Subject { get; set; }
    [Required, StringLength(3000)] public string Message { get; set; } = "";
}

public class TrackViewModel
{
    [Required, Display(Name = "Order number")] public string? Number { get; set; }
    [EmailAddress] public string? Email { get; set; }
    public string? Key { get; set; }
    public Order? Order { get; set; }
}
