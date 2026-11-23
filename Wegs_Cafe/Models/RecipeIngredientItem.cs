using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>Represents a single raw ingredient used per serving in a product recipe.</summary>
public class RecipeIngredientItem
{
    [JsonProperty("ingredient_identifier")]
    public string IngredientIdentifier { get; set; } = string.Empty;

    [JsonProperty("ingredient_name")]
    public string IngredientName { get; set; } = string.Empty;

    [JsonProperty("quantity_required")]
    public decimal QuantityRequired { get; set; }

    [JsonProperty("measurement_unit")]
    public string MeasurementUnit { get; set; } = MeasurementUnits.Milliliters;

    [JsonIgnore]
    public string QuantityDisplay => $"{QuantityRequired:0.##} {MeasurementUnit}";

    [JsonIgnore]
    public string SummaryDisplay => $"{IngredientName} ({QuantityRequired:0.##} {MeasurementUnit})";

    public RecipeIngredientItem Clone() => (RecipeIngredientItem)MemberwiseClone();
}
