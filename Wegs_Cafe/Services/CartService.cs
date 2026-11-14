using System.Collections.ObjectModel;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>In-memory cart shared across pages (registered as Singleton).</summary>
public class CartService
{
    public ObservableCollection<CartItem> Items { get; } = new();

    public event EventHandler? CartChanged;

    public decimal Total => Items.Sum(i => i.Subtotal);
    public int Count => Items.Sum(i => i.Quantity);

    public void Add(Product item, int quantity = 1)
    {
        var existing = Items.FirstOrDefault(c => c.Item.Id == item.Id);
        if (existing != null)
        {
            existing.Quantity += quantity;
            // replace to refresh UI bindings
            var index = Items.IndexOf(existing);
            Items[index] = existing;
        }
        else
        {
            Items.Add(new CartItem { Item = item, Quantity = quantity });
        }
        CartChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(CartItem cartItem)
    {
        Items.Remove(cartItem);
        CartChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        Items.Clear();
        CartChanged?.Invoke(this, EventArgs.Empty);
    }
}
