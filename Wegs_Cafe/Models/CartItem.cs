namespace Wegs_Cafe.Models;

/// <summary>Item in the local cart (kept in memory, not saved to Firebase).</summary>
public class CartItem
{
    public Product Item { get; set; } = new();
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }

    public decimal Subtotal => Item.Price * Quantity;
}
