using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodBooking.Core.Entities;

public class Category
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(120)] public string Slug { get; set; } = "";
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public List<Category> Children { get; set; } = new();
    [StringLength(500)] public string? Description { get; set; }
    [StringLength(300)] public string? ImageUrl { get; set; }
    [StringLength(10)] public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool ShowOnHome { get; set; } = true;
    public bool IsActive { get; set; } = true;
    [StringLength(200)] public string? MetaTitle { get; set; }
    [StringLength(500)] public string? MetaDescription { get; set; }
    public List<Product> Products { get; set; } = new();
}

public class Brand
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(120)] public string Slug { get; set; } = "";
    [StringLength(300)] public string? LogoUrl { get; set; }
    [StringLength(500)] public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Product> Products { get; set; } = new();
}

/// <summary>Admin-managed product tags such as "Latest", "Best Seller", "New", "Chef's Special".</summary>
public class Tag
{
    public int Id { get; set; }
    [Required, StringLength(60)] public string Name { get; set; } = "";
    [Required, StringLength(80)] public string Slug { get; set; } = "";
    [StringLength(200)] public string? Subtitle { get; set; }
    [StringLength(20)] public string BadgeColor { get; set; } = "#e63946";
    public bool ShowOnHome { get; set; } = true;
    public int HomeProductCount { get; set; } = 8;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ProductTag> ProductTags { get; set; } = new();
}

public class Product
{
    public int Id { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [Required, StringLength(220)] public string Slug { get; set; } = "";
    [StringLength(50)] public string? Sku { get; set; }
    [StringLength(500)] public string? ShortDescription { get; set; }
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")] public decimal Price { get; set; }
    /// <summary>Discounted price; when set and lower than Price it is the selling price.</summary>
    [Column(TypeName = "decimal(18,2)")] public decimal? SalePrice { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }

    public bool TrackStock { get; set; }
    public int StockQuantity { get; set; }
    public bool IsVegetarian { get; set; }
    public bool IsSpicy { get; set; }
    public int PrepTimeMinutes { get; set; } = 20;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public int ViewCount { get; set; }
    public int SoldCount { get; set; }

    [StringLength(200)] public string? MetaTitle { get; set; }
    [StringLength(500)] public string? MetaDescription { get; set; }
    [StringLength(100)] public string? CloverItemId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<ProductImage> Images { get; set; } = new();
    public List<ProductTag> ProductTags { get; set; } = new();
    public List<ProductOption> Options { get; set; } = new();
    public List<ProductSpecification> Specifications { get; set; } = new();

    [NotMapped] public decimal SellingPrice => SalePrice.HasValue && SalePrice.Value > 0 && SalePrice.Value < Price ? SalePrice.Value : Price;
    [NotMapped] public bool OnSale => SellingPrice < Price;
    [NotMapped] public bool InStock => !TrackStock || StockQuantity > 0;
    [NotMapped] public string MainImage => Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url ?? "/img/no-image.svg";
}

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    [Required, StringLength(300)] public string Url { get; set; } = "";
    public int SortOrder { get; set; }
}

public class ProductTag
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int TagId { get; set; }
    public Tag? Tag { get; set; }
}

public enum AttributeInputType { SingleSelect = 0, MultiSelect = 1 }

/// <summary>Reusable product attribute such as "Size", "Spice Level", "Extra Toppings".</summary>
public class ProductAttribute
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public AttributeInputType InputType { get; set; }
    /// <summary>When true the customer must pick a value of this attribute (single-select only).</summary>
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    public List<AttributeValue> Values { get; set; } = new();
}

public class AttributeValue
{
    public int Id { get; set; }
    public int AttributeId { get; set; }
    public ProductAttribute? Attribute { get; set; }
    [Required, StringLength(100)] public string Value { get; set; } = "";
    public int SortOrder { get; set; }
}

/// <summary>An attribute value offered on a specific product, with its own price adjustment.</summary>
public class ProductOption
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int AttributeValueId { get; set; }
    public AttributeValue? AttributeValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PriceAdjustment { get; set; }
    public bool IsDefault { get; set; }
}

public class ProductSpecification
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(300)] public string Value { get; set; } = "";
    public int SortOrder { get; set; }
}
