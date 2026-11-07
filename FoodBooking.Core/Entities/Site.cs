using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodBooking.Core.Entities;

/// <summary>Single-row table holding all website configuration managed from the admin panel.</summary>
public class SiteSetting
{
    public int Id { get; set; }

    // Logo setup
    [StringLength(300)] public string? LogoUrl { get; set; }
    [StringLength(300)] public string? FooterLogoUrl { get; set; }
    [StringLength(300)] public string? FaviconUrl { get; set; }

    // Company setup
    [Required, StringLength(150)] public string CompanyName { get; set; } = "Foodie Hub";
    [StringLength(200)] public string? Tagline { get; set; }
    [StringLength(300)] public string? Address { get; set; }
    [StringLength(40)] public string? Phone { get; set; }
    [StringLength(150)] public string? Email { get; set; }
    [StringLength(1000)] public string? AboutText { get; set; }
    [StringLength(500)] public string? MapEmbedUrl { get; set; }
    [StringLength(300)] public string? FooterText { get; set; }
    [StringLength(10)] public string CurrencySymbol { get; set; } = "$";
    [StringLength(10)] public string CurrencyCode { get; set; } = "USD";
    [Column(TypeName = "decimal(5,2)")] public decimal TaxPercent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DeliveryCharge { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal FreeDeliveryAbove { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MinOrderAmount { get; set; }
    public bool EnableDelivery { get; set; } = true;
    public bool EnablePickup { get; set; } = true;
    public bool EnableGuestCheckout { get; set; } = true;
    public bool EnableTips { get; set; } = true;
    [StringLength(80)] public string TimeZoneId { get; set; } = "UTC";

    // SEO
    [StringLength(200)] public string? MetaTitle { get; set; }
    [StringLength(500)] public string? MetaDescription { get; set; }
    [StringLength(500)] public string? MetaKeywords { get; set; }
    [StringLength(2000)] public string? HeadScripts { get; set; }

    // Social media
    [StringLength(300)] public string? FacebookUrl { get; set; }
    [StringLength(300)] public string? InstagramUrl { get; set; }
    [StringLength(300)] public string? TwitterUrl { get; set; }
    [StringLength(300)] public string? YoutubeUrl { get; set; }
    [StringLength(300)] public string? LinkedinUrl { get; set; }
    [StringLength(300)] public string? TiktokUrl { get; set; }
    [StringLength(40)] public string? WhatsappNumber { get; set; }

    // Payment methods
    public bool EnableCashOnDelivery { get; set; } = true;
    public bool EnableCreditCard { get; set; } = true;
    public bool EnablePaypal { get; set; } = true;
    /// <summary>When true, card / PayPal payments are simulated (for local testing only).</summary>
    public bool PaymentTestMode { get; set; } = true;
    [StringLength(200)] public string? PaypalClientId { get; set; }
    [StringLength(200)] public string? PaypalSecret { get; set; }
    public bool PaypalSandbox { get; set; } = true;

    // Clover POS + Clover e-commerce gateway
    public bool CloverEnabled { get; set; }
    public bool CloverSandbox { get; set; } = true;
    [StringLength(100)] public string? CloverMerchantId { get; set; }
    [StringLength(200)] public string? CloverApiToken { get; set; }
    [StringLength(200)] public string? CloverEcommercePublicKey { get; set; }
    [StringLength(200)] public string? CloverEcommercePrivateKey { get; set; }
    public bool CloverPrintToKitchen { get; set; } = true;

    // SMS (Twilio)
    public bool SmsEnabled { get; set; }
    [StringLength(100)] public string? TwilioAccountSid { get; set; }
    [StringLength(100)] public string? TwilioAuthToken { get; set; }
    [StringLength(30)] public string? TwilioFromNumber { get; set; }
    [StringLength(30)] public string? AdminAlertPhone { get; set; }

    // Email (SMTP)
    public bool EmailEnabled { get; set; }
    [StringLength(150)] public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    [StringLength(150)] public string? SmtpUser { get; set; }
    [StringLength(150)] public string? SmtpPassword { get; set; }
    [StringLength(150)] public string? SmtpFromEmail { get; set; }
    [StringLength(150)] public string? AdminAlertEmail { get; set; }

    [StringLength(300)] public string? FrontSiteUrl { get; set; } = "http://localhost:5263";
    /// <summary>Optional site-wide notice shown in the top bar of the website.</summary>
    [StringLength(300)] public string? AnnouncementText { get; set; }
}

public class CmsPage
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Title { get; set; } = "";
    [Required, StringLength(160)] public string Slug { get; set; } = "";
    public string? Content { get; set; }
    [StringLength(200)] public string? MetaTitle { get; set; }
    [StringLength(500)] public string? MetaDescription { get; set; }
    public bool ShowInHeader { get; set; }
    public bool ShowInFooter { get; set; } = true;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public enum BannerPosition
{
    HomeSlider = 0,
    HomeMiddle = 1,
    HomeBottom = 2,
    HeaderTop = 3,
    MenuPageTop = 4,
    ProductPageSide = 5,
    CartPage = 6
}

public class Banner
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Title { get; set; } = "";
    [StringLength(300)] public string? SubTitle { get; set; }
    [StringLength(300)] public string? ImageUrl { get; set; }
    [StringLength(300)] public string? LinkUrl { get; set; }
    [StringLength(60)] public string? ButtonText { get; set; }
    public BannerPosition Position { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class Holiday
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    [Required, StringLength(150)] public string Title { get; set; } = "";
    [StringLength(300)] public string? Message { get; set; }
    /// <summary>When false the store opens with special hours instead of closing all day.</summary>
    public bool IsClosedAllDay { get; set; } = true;
    public TimeSpan? OpenTime { get; set; }
    public TimeSpan? CloseTime { get; set; }
}

public class BusinessHour
{
    public int Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan OpenTime { get; set; }
    public TimeSpan CloseTime { get; set; }
    public bool IsClosed { get; set; }
}

public class AdminUser
{
    public int Id { get; set; }
    [Required, StringLength(60)] public string Username { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    [StringLength(120)] public string? FullName { get; set; }
    [StringLength(150)] public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
}

public class ContactMessage
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required, StringLength(150)] public string Email { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(200)] public string? Subject { get; set; }
    [Required, StringLength(3000)] public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class NewsletterSubscriber
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum NotificationChannel { Email = 0, Sms = 1, CloverPos = 2, Payment = 3 }

/// <summary>Audit log of every outgoing email / SMS / POS call so the admin can see what was sent.</summary>
public class NotificationLog
{
    public int Id { get; set; }
    public NotificationChannel Channel { get; set; }
    [StringLength(200)] public string? Recipient { get; set; }
    [StringLength(300)] public string? Subject { get; set; }
    public string? Body { get; set; }
    public bool Success { get; set; }
    [StringLength(1000)] public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
