using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>products/{product_id}</summary>
public class Product
{
    [JsonProperty("product_id")] public string ProductId { get; set; } = string.Empty;
    [JsonProperty("sku")] public string Sku { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("category")] public string Category { get; set; } = ProductCategories.Coffee;
    [JsonProperty("price")] public decimal Price { get; set; }
    /// <summary>Hidden from the Manager and Staff roles (profit margin data).</summary>
    [JsonProperty("cost_price")] public decimal CostPrice { get; set; }
    [JsonProperty("stock_quantity")] public int StockQuantity { get; set; }
    [JsonProperty("low_stock_threshold")] public int LowStockThreshold { get; set; } = 10;
    [JsonProperty("unit")] public string Unit { get; set; } = "pcs";
    [JsonProperty("image_url")] public string ImageUrl { get; set; } = string.Empty;
    [JsonProperty("is_available")] public bool IsAvailable { get; set; } = true;
    [JsonProperty("updated_at")] public string UpdatedAt { get; set; } = IsoDate.Now();

    [JsonIgnore] public bool IsOutOfStock => StockQuantity <= 0;
    [JsonIgnore] public bool IsLowStock => StockQuantity > 0 && StockQuantity <= LowStockThreshold;
    [JsonIgnore] public decimal Profit => Price - CostPrice;
    [JsonIgnore] public string StatusText => IsOutOfStock ? "Out of stock" : IsLowStock ? "Low stock" : "In stock";
    [JsonIgnore] public string StockText => $"{StockQuantity} {Unit}";
    [JsonIgnore] public string SkuAndCategory => $"{Sku} · {Category}";

    public Product Clone() => (Product)MemberwiseClone();
}

