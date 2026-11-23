using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>Firebase schema: ingredients/{ingredient_identifier}</summary>
public class Ingredient
{
    [JsonProperty("ingredient_identifier")]
    public string IngredientIdentifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Stock Keeping Unit (SKU) is required.")]
    [RegularExpression(@"^[A-Z0-9\-]{3,20}$", ErrorMessage = "SKU must be 3 to 20 uppercase letters, numbers, or hyphens.")]
    [JsonProperty("stock_keeping_unit")]
    public string StockKeepingUnit { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingredient name is required.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "Ingredient name must be between 2 and 60 characters.")]
    [JsonProperty("ingredient_name")]
    public string IngredientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [JsonProperty("category")]
    public string Category { get; set; } = IngredientCategories.DairyAndMilk;

    [Required(ErrorMessage = "Measurement unit is required.")]
    [JsonProperty("measurement_unit")]
    public string MeasurementUnit { get; set; } = MeasurementUnits.Milliliters;

    [Range(0, 9999999.99, ErrorMessage = "Current stock quantity cannot be negative.")]
    [JsonProperty("current_stock_quantity")]
    public decimal CurrentStockQuantity { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "Low stock threshold must be at least 0.01.")]
    [JsonProperty("low_stock_threshold")]
    public decimal LowStockThreshold { get; set; } = 100m;

    [Range(0, 999999.99, ErrorMessage = "Unit cost cannot be negative.")]
    [JsonProperty("unit_cost")]
    public decimal UnitCost { get; set; }

    [JsonProperty("updated_at")]
    public string UpdatedAt { get; set; } = IsoTimestamp.Now();

    [JsonIgnore]
    public bool IsOutOfStock => CurrentStockQuantity <= 0;

    [JsonIgnore]
    public bool IsLowStock => CurrentStockQuantity > 0 && CurrentStockQuantity <= LowStockThreshold;

    [JsonIgnore]
    public string StockQuantityDisplay => $"{CurrentStockQuantity:0.##} {MeasurementUnit}";

    [JsonIgnore]
    public string StockDisplayText => StockQuantityDisplay;

    [JsonIgnore]
    public string StockKeepingUnitAndCategory => $"{StockKeepingUnit} · {Category}";

    [JsonIgnore]
    public string StockStatusLabel => IsOutOfStock ? "Out of Stock" : IsLowStock ? "Low Stock" : "In Stock";

    [JsonIgnore]
    public string StockStatusText => StockStatusLabel;

    [JsonIgnore]
    public decimal TotalValuationAtCost => UnitCost * Math.Max(0m, CurrentStockQuantity);

    public Ingredient Clone() => (Ingredient)MemberwiseClone();
}
