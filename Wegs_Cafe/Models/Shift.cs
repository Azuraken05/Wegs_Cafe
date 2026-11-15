using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>shifts/{shift_id} - cash drawer shift. "open" = X reading available, "closed" = Z reading done.</summary>
public class Shift
{
    [JsonProperty("shift_id")] public string ShiftId { get; set; } = string.Empty;
    [JsonProperty("status")] public string Status { get; set; } = "open";          // "open" | "closed"
    [JsonProperty("opened_by_uid")] public string OpenedByUid { get; set; } = string.Empty;
    [JsonProperty("opened_by_name")] public string OpenedByName { get; set; } = string.Empty;
    [JsonProperty("opening_cash")] public decimal OpeningCash { get; set; }
    [JsonProperty("opened_at")] public string OpenedAt { get; set; } = IsoDate.Now();

    // Filled when the shift is closed (Z reading)
    [JsonProperty("closed_by_uid")] public string ClosedByUid { get; set; } = string.Empty;
    [JsonProperty("closed_by_name")] public string ClosedByName { get; set; } = string.Empty;
    [JsonProperty("closed_at")] public string ClosedAt { get; set; } = string.Empty;
    [JsonProperty("order_count")] public int OrderCount { get; set; }
    [JsonProperty("total_sales")] public decimal TotalSales { get; set; }
    [JsonProperty("cash_sales")] public decimal CashSales { get; set; }
    [JsonProperty("gcash_sales")] public decimal GCashSales { get; set; }
    [JsonProperty("card_sales")] public decimal CardSales { get; set; }
    [JsonProperty("expected_cash")] public decimal ExpectedCash { get; set; }
    [JsonProperty("actual_cash")] public decimal ActualCash { get; set; }
    [JsonProperty("difference")] public decimal Difference { get; set; }           // actual - expected (negative = short)
}

/// <summary>Live totals for an open shift (X reading). Not stored.</summary>
public class ShiftReading
{
    public int OrderCount { get; set; }
    public int VoidedCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal CashSales { get; set; }
    public decimal GCashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal ExpectedCash => OpeningCash + CashSales;
}
