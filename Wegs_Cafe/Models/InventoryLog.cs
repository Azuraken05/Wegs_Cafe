using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>Firebase schema: inventory_logs/{inventory_log_identifier}</summary>
public class InventoryLog
{
    [JsonProperty("inventory_log_identifier")]
    public string InventoryLogIdentifier { get; set; } = string.Empty;

    [JsonProperty("product_identifier")]
    public string ProductIdentifier { get; set; } = string.Empty;

    [JsonProperty("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [JsonProperty("movement_type")]
    public string MovementType { get; set; } = InventoryMovementTypes.ManualCorrection;

    [JsonProperty("quantity_changed")]
    public int QuantityChanged { get; set; }

    [JsonProperty("previous_stock_quantity")]
    public int PreviousStockQuantity { get; set; }

    [JsonProperty("new_stock_quantity")]
    public int NewStockQuantity { get; set; }

    [JsonProperty("remarks")]
    public string Remarks { get; set; } = string.Empty;

    [JsonProperty("performed_by_identifier")]
    public string PerformedByIdentifier { get; set; } = string.Empty;

    [JsonProperty("performed_by_name")]
    public string PerformedByName { get; set; } = string.Empty;

    [JsonProperty("recorded_at")]
    public string RecordedAt { get; set; } = IsoTimestamp.Now();

    [JsonIgnore]
    public string RecordedAtLocalDisplay => IsoTimestamp.ToLocal(RecordedAt)?.ToString("MMM d, yyyy h:mm tt") ?? RecordedAt;

    [JsonIgnore]
    public string QuantityChangedDisplay => QuantityChanged >= 0
        ? $"+{QuantityChanged} ({PreviousStockQuantity} -> {NewStockQuantity})"
        : $"{QuantityChanged} ({PreviousStockQuantity} -> {NewStockQuantity})";
}
