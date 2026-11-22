using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>Firebase schema: products/{product_identifier}</summary>
public class Product
{
    [JsonProperty("product_identifier")]
    public string ProductIdentifier { get; set; } = string.Empty;

    [JsonProperty("stock_keeping_unit")]
    public string StockKeepingUnit { get; set; } = string.Empty;

    [JsonProperty("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [JsonProperty("category")]
    public string Category { get; set; } = ProductCategories.Coffee;

    [JsonProperty("selling_price")]
    public decimal SellingPrice { get; set; }

    [JsonProperty("cost_price")]
    public decimal CostPrice { get; set; }

    [JsonProperty("stock_quantity")]
    public int StockQuantity { get; set; }

    [JsonProperty("low_stock_threshold")]
    public int LowStockThreshold { get; set; } = 10;

    [JsonProperty("measurement_unit")]
    public string MeasurementUnit { get; set; } = "cups";

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
    public string StockStatusLabel => IsOutOfStock ? "Out of Stock" : IsLowStock ? "Low Stock" : "In Stock";

    [JsonIgnore]
    public string StockStatusText => StockStatusLabel;

    public Product Clone() => (Product)MemberwiseClone();
}
