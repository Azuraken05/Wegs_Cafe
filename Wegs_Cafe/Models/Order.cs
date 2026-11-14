namespace Wegs_Cafe.Models;

public enum OrderStatus
{
    Pending,
    Preparing,
    Ready,
    Completed,
    Cancelled
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;   // copied so history stays correct if menu changes
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }

    public decimal Subtotal => UnitPrice * Quantity;
}

public class Order : BaseModel
{
    public string UserId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? Notes { get; set; }
}
