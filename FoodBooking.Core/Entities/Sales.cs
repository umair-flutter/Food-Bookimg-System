using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodBooking.Core.Entities;

public class Customer
{
    public int Id { get; set; }
    [Required, StringLength(60)] public string FirstName { get; set; } = "";
    [StringLength(60)] public string? LastName { get; set; }
    [Required, StringLength(150)] public string Email { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    [Required] public string PasswordHash { get; set; } = "";
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(80)] public string? City { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
    public bool IsActive { get; set; } = true;
    [StringLength(100)] public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public List<Order> Orders { get; set; } = new();

    [NotMapped] public string FullName => $"{FirstName} {LastName}".Trim();
}

public enum OrderStatus { Pending = 0, Confirmed = 1, Preparing = 2, ReadyForPickup = 3, OutForDelivery = 4, Delivered = 5, Cancelled = 6 }
public enum OrderType { Delivery = 0, Pickup = 1 }
public enum PaymentMethod { CashOnDelivery = 0, CreditCard = 1, PayPal = 2 }
public enum PaymentStatus { Unpaid = 0, Paid = 1, Failed = 2, Refunded = 3 }

public class Order
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string OrderNumber { get; set; } = "";
    /// <summary>Secret used for guest access to invoice / tracking links.</summary>
    [Required, StringLength(40)] public string AccessKey { get; set; } = Guid.NewGuid().ToString("N");
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public bool IsGuest { get; set; }

    [Required, StringLength(120)] public string CustomerName { get; set; } = "";
    [Required, StringLength(150)] public string Email { get; set; } = "";
    [Required, StringLength(30)] public string Phone { get; set; } = "";
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(80)] public string? City { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
    [StringLength(500)] public string? Notes { get; set; }

    public OrderType OrderType { get; set; }
    public DateTime? ScheduledFor { get; set; }

    [Column(TypeName = "decimal(18,2)")] public decimal SubTotal { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Discount { get; set; }
    [StringLength(40)] public string? CouponCode { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Tax { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DeliveryCharge { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Tip { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Total { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    [StringLength(150)] public string? TransactionId { get; set; }
    public OrderStatus Status { get; set; }

    [StringLength(100)] public string? CloverOrderId { get; set; }
    [StringLength(500)] public string? CloverSyncError { get; set; }
    public bool KitchenAcknowledged { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<OrderItem> Items { get; set; } = new();
    public List<OrderStatusHistory> History { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    [Required, StringLength(200)] public string ProductName { get; set; } = "";
    [StringLength(300)] public string? ImageUrl { get; set; }
    [StringLength(500)] public string? OptionsText { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal LineTotal { get; set; }
}

public class OrderStatusHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public OrderStatus Status { get; set; }
    [StringLength(500)] public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum DiscountType { Percentage = 0, FixedAmount = 1 }

public class Coupon
{
    public int Id { get; set; }
    [Required, StringLength(40)] public string Code { get; set; } = "";
    [StringLength(200)] public string? Description { get; set; }
    public DiscountType DiscountType { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Value { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MinOrderAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? MaxDiscount { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;
}
