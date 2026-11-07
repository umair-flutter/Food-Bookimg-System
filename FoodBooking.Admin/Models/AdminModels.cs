using System.ComponentModel.DataAnnotations;
using FoodBooking.Core.Entities;

namespace FoodBooking.Admin.Models;

public class LoginModel
{
    [Required] public string Username { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

public class ChangePasswordModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password), Display(Name = "New password")] public string NewPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword)), Display(Name = "Confirm new password")] public string ConfirmPassword { get; set; } = "";
}

public class PagedList<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int Total { get; set; }
    public int Pages => (int)Math.Ceiling(Total / (double)PageSize);
}

public class DashboardModel
{
    public int OrdersToday { get; set; }
    public decimal RevenueToday { get; set; }
    public int PendingOrders { get; set; }
    public int OrdersMonth { get; set; }
    public decimal RevenueMonth { get; set; }
    public int Customers { get; set; }
    public int Products { get; set; }
    public int UnreadMessages { get; set; }
    public List<Order> RecentOrders { get; set; } = new();
    public List<(string name, int qty, decimal revenue)> TopProducts { get; set; } = new();
    public List<(string day, int orders, decimal revenue)> Last14Days { get; set; } = new();
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public List<Product> LowStock { get; set; } = new();
    public List<string> SetupWarnings { get; set; } = new();
}

public class ProductForm
{
    public int Id { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [StringLength(220)] public string? Slug { get; set; }
    [StringLength(50)] public string? Sku { get; set; }
    [StringLength(500), Display(Name = "Short description")] public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    [Range(0, 1000000)] public decimal Price { get; set; }
    [Range(0, 1000000), Display(Name = "Sale price")] public decimal? SalePrice { get; set; }
    [Required, Display(Name = "Category")] public int CategoryId { get; set; }
    [Display(Name = "Brand")] public int? BrandId { get; set; }
    [Display(Name = "Track stock")] public bool TrackStock { get; set; }
    [Display(Name = "Stock quantity")] public int StockQuantity { get; set; }
    [Display(Name = "Vegetarian")] public bool IsVegetarian { get; set; }
    [Display(Name = "Spicy")] public bool IsSpicy { get; set; }
    [Range(0, 600), Display(Name = "Prep time (min)")] public int PrepTimeMinutes { get; set; } = 20;
    [Display(Name = "Active (visible on website)")] public bool IsActive { get; set; } = true;
    [Display(Name = "Sort order")] public int SortOrder { get; set; }
    [StringLength(200), Display(Name = "Meta title")] public string? MetaTitle { get; set; }
    [StringLength(500), Display(Name = "Meta description")] public string? MetaDescription { get; set; }
    [StringLength(100), Display(Name = "Clover item ID")] public string? CloverItemId { get; set; }
    public List<int> TagIds { get; set; } = new();
    public List<OptionRow> Options { get; set; } = new();
    public List<SpecRow> Specs { get; set; } = new();
    public List<ProductImage> Images { get; set; } = new();
}

public class OptionRow
{
    public int AttributeValueId { get; set; }
    public bool Enabled { get; set; }
    public decimal PriceAdjustment { get; set; }
    public bool IsDefault { get; set; }
}

public class SpecRow
{
    public string? Name { get; set; }
    public string? Value { get; set; }
}

public class OrderFilter
{
    public string? Q { get; set; }
    public OrderStatus? Status { get; set; }
    public PaymentMethod? Payment { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public OrderType? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
}
