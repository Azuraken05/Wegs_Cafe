using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>Firebase schema: inventory_logs/{inventory_log_identifier}</summary>
public class InventoryLog
{
    [JsonProperty("inventory_log_identifier")]
    public string InventoryLogIdentifier { get; set; } = string.Empty;

    [JsonProperty("ingredient_identifier")]
    public string IngredientIdentifier { get; set; } = string.Empty;

    [JsonProperty("ingredient_name")]
    public string IngredientName { get; set; } = string.Empty;

    [JsonProperty("product_identifier")]
    public string ProductIdentifier { get; set; } = string.Empty;

    [JsonProperty("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [JsonProperty("measurement_unit")]
    public string MeasurementUnit { get; set; } = string.Empty;

    [JsonProperty("movement_type")]
    public string MovementType { get; set; } = InventoryMovementTypes.ManualCorrection;

    [JsonProperty("quantity_changed")]
    public decimal QuantityChanged { get; set; }

    [JsonProperty("previous_stock_quantity")]
    public decimal PreviousStockQuantity { get; set; }

    [JsonProperty("new_stock_quantity")]
    public decimal NewStockQuantity { get; set; }

    [JsonProperty("reference_receipt_number")]
    public string ReferenceReceiptNumber { get; set; } = string.Empty;

    [JsonProperty("remarks")]
    public string Remarks { get; set; } = string.Empty;

    [JsonProperty("performed_by_identifier")]
    public string PerformedByIdentifier { get; set; } = string.Empty;

    [JsonProperty("performed_by_name")]
    public string PerformedByName { get; set; } = string.Empty;

    [JsonProperty("recorded_at")]
    public string RecordedAt { get; set; } = IsoTimestamp.Now();

    [JsonIgnore]
    public string ItemDisplayName => !string.IsNullOrWhiteSpace(IngredientName) ? IngredientName : ProductName;

    [JsonIgnore]
    public string RecordedAtLocalDisplay => IsoTimestamp.ToLocal(RecordedAt)?.ToString("MMM d, yyyy h:mm tt") ?? RecordedAt;

    [JsonIgnore]
    public string QuantityChangedDisplay
    {
        get
        {
            var unitSuffix = string.IsNullOrWhiteSpace(MeasurementUnit) ? string.Empty : $" {MeasurementUnit}";
            return QuantityChanged >= 0
                ? $"+{QuantityChanged:0.##}{unitSuffix} ({PreviousStockQuantity:0.##} -> {NewStockQuantity:0.##})".Trim()
                : $"{QuantityChanged:0.##}{unitSuffix} ({PreviousStockQuantity:0.##} -> {NewStockQuantity:0.##})".Trim();
        }
    }
}
