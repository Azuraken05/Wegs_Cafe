using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>Line item inside orders/{order_identifier}/order_items</summary>
public class OrderItem
{
    [JsonProperty("product_identifier")]
    public string ProductIdentifier { get; set; } = string.Empty;

    [JsonProperty("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonProperty("line_subtotal")]
    public decimal LineSubtotal { get; set; }

    [JsonProperty("cup_size")]
    public string CupSize { get; set; } = "Regular";

    [JsonProperty("ice_level")]
    public string IceLevel { get; set; } = "Regular Ice";

    [JsonProperty("sweetness_level")]
    public string SweetnessLevel { get; set; } = "100%";

    [JsonProperty("add_ons")]
    public List<string> AddOns { get; set; } = new();

    [JsonProperty("modifier_surcharge")]
    public decimal ModifierSurcharge { get; set; }

    [JsonProperty("preparation_notes")]
    public string PreparationNotes { get; set; } = string.Empty;
}

/// <summary>Firebase schema: orders/{order_identifier}</summary>
public class Order
{
    [JsonProperty("order_identifier")]
    public string OrderIdentifier { get; set; } = string.Empty;

    [JsonProperty("receipt_number")]
    public string ReceiptNumber { get; set; } = string.Empty;

    [JsonProperty("order_type")]
    public string OrderType { get; set; } = OrderTypes.DineIn;

    [JsonProperty("customer_or_table_label")]
    public string CustomerOrTableLabel { get; set; } = string.Empty;

    [JsonProperty("preparation_status")]
    public string PreparationStatus { get; set; } = OrderPreparationStatuses.Preparing;

    [JsonProperty("order_items")]
    public List<OrderItem> OrderItems { get; set; } = new();

    [JsonProperty("subtotal_amount")]
    public decimal SubtotalAmount { get; set; }

    [JsonProperty("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [JsonProperty("total_amount")]
    public decimal TotalAmount { get; set; }

    [JsonProperty("payment_method")]
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;

    [JsonProperty("payment_reference_number")]
    public string PaymentReferenceNumber { get; set; } = string.Empty;

    [JsonProperty("amount_tendered")]
    public decimal AmountTendered { get; set; }

    [JsonProperty("change_due")]
    public decimal ChangeDue { get; set; }

    [JsonProperty("order_status")]
    public string OrderStatus { get; set; } = OrderStatuses.Completed;

    [JsonProperty("barista_identifier")]
    public string BaristaIdentifier { get; set; } = string.Empty;

    [JsonProperty("barista_name")]
    public string BaristaName { get; set; } = string.Empty;

    /// <summary>Compatibility alias mapped to BaristaIdentifier.</summary>
    [JsonProperty("cashier_identifier")]
    public string CashierIdentifier
    {
        get => BaristaIdentifier;
        set => BaristaIdentifier = value;
    }

    /// <summary>Compatibility alias mapped to BaristaName.</summary>
    [JsonProperty("cashier_name")]
    public string CashierName
    {
        get => BaristaName;
        set => BaristaName = value;
    }

    [JsonProperty("created_at")]
    public string CreatedAt { get; set; } = IsoTimestamp.Now();

    [JsonProperty("voided_at")]
    public string VoidedAt { get; set; } = string.Empty;

    [JsonProperty("voided_by_identifier")]
    public string VoidedByIdentifier { get; set; } = string.Empty;

    [JsonProperty("voided_by_name")]
    public string VoidedByName { get; set; } = string.Empty;

    [JsonProperty("void_remarks")]
    public string VoidRemarks { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsVoided => OrderStatus == OrderStatuses.Voided;

    [JsonIgnore]
    public bool IsNotVoided => !IsVoided;

    [JsonIgnore]
    public bool IsPreparing => PreparationStatus == OrderPreparationStatuses.Preparing;

    [JsonIgnore]
    public bool IsReadyForPickup => PreparationStatus == OrderPreparationStatuses.Ready;

    [JsonIgnore]
    public string CreatedAtLocalDisplay => IsoTimestamp.ToLocal(CreatedAt)?.ToString("MMM d, yyyy h:mm tt") ?? CreatedAt;

    [JsonIgnore]
    public string OrderItemsSummary => string.Join(", ", OrderItems.Select(item => $"{item.Quantity}x {item.ProductName}"));

    [JsonIgnore]
    public string ItemsSummary => OrderItemsSummary;
}
