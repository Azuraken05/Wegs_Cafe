using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>Firebase schema: shifts/{shift_identifier} — End-of-Day X/Z cash reconciliation.</summary>
public class ShiftSession
{
    [JsonProperty("shift_identifier")]
    public string ShiftIdentifier { get; set; } = string.Empty;

    [JsonProperty("shift_status")]
    public string ShiftStatus { get; set; } = "open";

    [JsonProperty("opened_by_identifier")]
    public string OpenedByIdentifier { get; set; } = string.Empty;

    [JsonProperty("opened_by_name")]
    public string OpenedByName { get; set; } = string.Empty;

    [JsonProperty("opening_cash_amount")]
    public decimal OpeningCashAmount { get; set; }

    [JsonProperty("opened_at")]
    public string OpenedAt { get; set; } = IsoTimestamp.Now();

    [JsonProperty("closed_by_identifier")]
    public string ClosedByIdentifier { get; set; } = string.Empty;

    [JsonProperty("closed_by_name")]
    public string ClosedByName { get; set; } = string.Empty;

    [JsonProperty("closed_at")]
    public string ClosedAt { get; set; } = string.Empty;

    [JsonProperty("completed_order_count")]
    public int CompletedOrderCount { get; set; }

    [JsonProperty("total_sales_amount")]
    public decimal TotalSalesAmount { get; set; }

    [JsonProperty("cash_sales_amount")]
    public decimal CashSalesAmount { get; set; }

    [JsonProperty("gcash_sales_amount")]
    public decimal GCashSalesAmount { get; set; }

    [JsonProperty("credit_card_sales_amount")]
    public decimal CreditCardSalesAmount { get; set; }

    [JsonProperty("expected_cash_amount")]
    public decimal ExpectedCashAmount { get; set; }

    [JsonProperty("actual_cash_amount")]
    public decimal ActualCashAmount { get; set; }

    [JsonProperty("cash_difference_amount")]
    public decimal CashDifferenceAmount { get; set; }
}
