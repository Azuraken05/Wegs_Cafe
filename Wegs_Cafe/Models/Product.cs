using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>Firebase schema: products/{product_identifier}</summary>
public class Product
{
    [JsonProperty("product_identifier")]
    public string ProductIdentifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Stock Keeping Unit (SKU) is required.")]
    [RegularExpression(@"^[A-Z0-9\-]{3,20}$", ErrorMessage = "SKU must be 3 to 20 uppercase letters, numbers, or hyphens.")]
    [JsonProperty("stock_keeping_unit")]
    public string StockKeepingUnit { get; set; } = string.Empty;

    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Product name must be between 2 and 80 characters.")]
    [JsonProperty("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Product category is required.")]
    [JsonProperty("category")]
    public string Category { get; set; } = ProductCategories.HotCoffee;

    [Range(0.01, 999999.99, ErrorMessage = "Selling price must be greater than 0.00.")]
    [JsonProperty("selling_price")]
    public decimal SellingPrice { get; set; }

    [Range(0.00, 999999.99, ErrorMessage = "Cost price cannot be negative.")]
    [JsonProperty("cost_price")]
    public decimal CostPrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Initial stock quantity cannot be negative.")]
    [JsonProperty("stock_quantity")]
    public int StockQuantity { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Low-stock threshold must be at least 1.")]
    [JsonProperty("low_stock_threshold")]
    public int LowStockThreshold { get; set; } = 10;

    [JsonProperty("measurement_unit")]
    public string MeasurementUnit { get; set; } = "cups";

    [JsonProperty("allow_drink_modifiers")]
    public bool AllowDrinkModifiers { get; set; } = true;

    [JsonProperty("image_url")]
    public string ImageUrl { get; set; } = string.Empty;

    [JsonProperty("is_available")]
    public bool IsAvailable { get; set; } = true;

    [JsonProperty("updated_at")]
    public string UpdatedAt { get; set; } = IsoTimestamp.Now();

    [JsonIgnore]
    public bool IsOutOfStock => StockQuantity <= 0;

    [JsonIgnore]
    public bool IsLowStock => StockQuantity > 0 && StockQuantity <= LowStockThreshold;

    [JsonIgnore]
    public decimal UnitProfit => SellingPrice - CostPrice;

    [JsonIgnore]
    public decimal InventoryValuationAtCost => CostPrice * Math.Max(0, StockQuantity);

    [JsonIgnore]
    public decimal InventoryValuationAtRetail => SellingPrice * Math.Max(0, StockQuantity);

    [JsonIgnore]
    public string StockQuantityDisplay => $"{StockQuantity} {MeasurementUnit}";

    [JsonIgnore]
    public string StockDisplayText => StockQuantityDisplay;

    [JsonIgnore]
    public string StockKeepingUnitAndCategory => $"{StockKeepingUnit} · {Category}";

    [JsonIgnore]
    public string StockStatusLabel => !IsAvailable ? "Sold Out" : IsOutOfStock ? "Out of Stock" : IsLowStock ? "Low Stock" : "In Stock";

    [JsonIgnore]
    public string StockStatusText => StockStatusLabel;

    public Product Clone() => (Product)MemberwiseClone();
}
