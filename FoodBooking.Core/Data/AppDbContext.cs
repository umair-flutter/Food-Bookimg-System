using FoodBooking.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodBooking.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<AttributeValue> AttributeValues => Set<AttributeValue>();
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    public DbSet<ProductSpecification> ProductSpecifications => Set<ProductSpecification>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<CmsPage> CmsPages => Set<CmsPage>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    /// <summary>SQLite (hosted demo) can't sort or compare decimals, so store them as REAL there.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        if (Database.IsSqlite()) configurationBuilder.Properties<decimal>().HaveConversion<double>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Category>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Category>().HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Brand>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Tag>().HasIndex(x => x.Slug).IsUnique();

        b.Entity<Product>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Product>().HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Product>().HasOne(x => x.Brand).WithMany(x => x.Products).HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<ProductTag>().HasKey(x => new { x.ProductId, x.TagId });
        b.Entity<ProductOption>().HasOne(x => x.AttributeValue).WithMany().HasForeignKey(x => x.AttributeValueId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Customer>().HasIndex(x => x.Email).IsUnique();
        b.Entity<AdminUser>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Coupon>().HasIndex(x => x.Code).IsUnique();
        b.Entity<CmsPage>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<NewsletterSubscriber>().HasIndex(x => x.Email).IsUnique();

        b.Entity<Order>().HasIndex(x => x.OrderNumber).IsUnique();
        b.Entity<Order>().HasIndex(x => x.CreatedAt);
        b.Entity<Order>().HasOne(x => x.Customer).WithMany(x => x.Orders).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<OrderItem>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
    }
}
