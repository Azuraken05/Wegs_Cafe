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

    [JsonProperty("cashier_identifier")]
    public string CashierIdentifier { get; set; } = string.Empty;

    [JsonProperty("cashier_name")]
    public string CashierName { get; set; } = string.Empty;

    [JsonProperty("created_at")]
    public string CreatedAt { get; set; } = IsoTimestamp.Now();

    [JsonProperty("voided_at")]
    public string VoidedAt { get; set; } = string.Empty;

    [JsonProperty("voided_by_identifier")]
    public string VoidedByIdentifier { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsVoided => OrderStatus == OrderStatuses.Voided;

    [JsonIgnore]
    public bool IsNotVoided => !IsVoided;

    [JsonIgnore]
    public string CreatedAtLocalDisplay => IsoTimestamp.ToLocal(CreatedAt)?.ToString("MMM d, yyyy h:mm tt") ?? CreatedAt;

    [JsonIgnore]
    public string OrderItemsSummary => string.Join(", ", OrderItems.Select(item => $"{item.Quantity}x {item.ProductName}"));

    [JsonIgnore]
    public string ItemsSummary => OrderItemsSummary;
}
