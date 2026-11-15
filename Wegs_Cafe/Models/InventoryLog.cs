using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>inventory_logs/{log_id}</summary>
public class InventoryLog
{
    [JsonProperty("log_id")] public string LogId { get; set; } = string.Empty;
    [JsonProperty("product_id")] public string ProductId { get; set; } = string.Empty;
    [JsonProperty("product_name")] public string ProductName { get; set; } = string.Empty;
    [JsonProperty("change_type")] public string ChangeType { get; set; } = InventoryChangeTypes.ManualAdjustment;
    /// <summary>Signed: -2 for a sale, +50 for a restock.</summary>
    [JsonProperty("quantity_changed")] public int QuantityChanged { get; set; }
    [JsonProperty("previous_stock")] public int PreviousStock { get; set; }
    [JsonProperty("new_stock")] public int NewStock { get; set; }
    [JsonProperty("performed_by_uid")] public string PerformedByUid { get; set; } = string.Empty;
    [JsonProperty("performed_by_name")] public string PerformedByName { get; set; } = string.Empty;
    [JsonProperty("timestamp")] public string Timestamp { get; set; } = IsoDate.Now();
}
