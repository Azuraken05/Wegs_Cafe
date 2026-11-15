using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>The current order being built at the POS (one shared cart for the device).</summary>
public class CartService : ObservableObject
{
    public ObservableCollection<CartItem> Items { get; } = new();

    public decimal Subtotal => Items.Sum(i => i.Subtotal);
    public int Count => Items.Sum(i => i.Qty);
    public bool HasItems => Items.Count > 0;

    public CartService()
    {
        Items.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null) foreach (CartItem i in e.NewItems) i.PropertyChanged += OnItemChanged;
            if (e.OldItems != null) foreach (CartItem i in e.OldItems) i.PropertyChanged -= OnItemChanged;
            Refresh();
        };
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasItems));
    }

    /// <summary>Returns an error message when the stock limit is reached, otherwise null.</summary>
    public string? Add(Product product)
    {
        if (!product.IsAvailable) return $"{product.Name} is not available.";
        var line = Items.FirstOrDefault(i => i.Product.ProductId == product.ProductId);
        var wanted = (line?.Qty ?? 0) + 1;
        if (wanted > product.StockQuantity)
            return product.StockQuantity <= 0 ? $"{product.Name} is out of stock." : $"Only {product.StockQuantity} {product.Unit} of {product.Name} left.";

        if (line == null) Items.Add(new CartItem { Product = product, Qty = 1 });
        else line.Qty = wanted;
        return null;
    }

    public string? Increment(CartItem line)
    {
        if (line.Qty + 1 > line.Product.StockQuantity) return $"Only {line.Product.StockQuantity} {line.Product.Unit} of {line.Product.Name} left.";
        line.Qty++;
        return null;
    }

    public void Decrement(CartItem line)
    {
        if (line.Qty <= 1) Items.Remove(line);
        else line.Qty--;
    }

    public void Remove(CartItem line) => Items.Remove(line);
    public void Clear() => Items.Clear();
}
