using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>One line inside orders/{order_id}/items (stored as an array).</summary>
public class OrderItem
{
    [JsonProperty("product_id")] public string ProductId { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("qty")] public int Qty { get; set; }
    [JsonProperty("unit_price")] public decimal UnitPrice { get; set; }
    [JsonProperty("subtotal")] public decimal Subtotal { get; set; }
    [JsonProperty("notes")] public string Notes { get; set; } = string.Empty;
}

/// <summary>orders/{order_id}</summary>
public class Order
{
    [JsonProperty("order_id")] public string OrderId { get; set; } = string.Empty;
    [JsonProperty("receipt_number")] public string ReceiptNumber { get; set; } = string.Empty;   // WEGS-yyyyMMdd-0001
    [JsonProperty("order_type")] public string OrderType { get; set; } = OrderTypes.DineIn;
    [JsonProperty("table_or_customer")] public string TableOrCustomer { get; set; } = string.Empty;
    [JsonProperty("items")] public List<OrderItem> Items { get; set; } = new();
    [JsonProperty("subtotal")] public decimal Subtotal { get; set; }
    [JsonProperty("discount_amount")] public decimal DiscountAmount { get; set; }
    [JsonProperty("total_amount")] public decimal TotalAmount { get; set; }
    [JsonProperty("payment_method")] public string PaymentMethod { get; set; } = PaymentMethods.Cash;
    /// <summary>Required for GCash (ref #) and Credit Card (approval/trace #).</summary>
    [JsonProperty("payment_reference")] public string PaymentReference { get; set; } = string.Empty;
    /// <summary>Last 4 digits of the card (card payments only).</summary>
    [JsonProperty("card_last4")] public string CardLast4 { get; set; } = string.Empty;
    [JsonProperty("amount_tendered")] public decimal AmountTendered { get; set; }
    [JsonProperty("change_due")] public decimal ChangeDue { get; set; }
    [JsonProperty("status")] public string Status { get; set; } = OrderStatuses.Completed;
    [JsonProperty("cashier_uid")] public string CashierUid { get; set; } = string.Empty;
    [JsonProperty("cashier_name")] public string CashierName { get; set; } = string.Empty;
    [JsonProperty("created_at")] public string CreatedAt { get; set; } = IsoDate.Now();
    [JsonProperty("voided_at")] public string VoidedAt { get; set; } = string.Empty;
    [JsonProperty("voided_by_uid")] public string VoidedByUid { get; set; } = string.Empty;

    [JsonIgnore] public bool IsVoided => Status == OrderStatuses.Voided;
    [JsonIgnore] public bool IsNotVoided => !IsVoided;
    [JsonIgnore] public string CreatedDisplay => IsoDate.ToLocal(CreatedAt)?.ToString("MMM d, h:mm tt") ?? CreatedAt;
    [JsonIgnore] public string ItemsSummary => string.Join(", ", Items.Select(i => $"{i.Qty}x {i.Name}"));
}
