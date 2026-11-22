using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Shared in-memory Point of Sale cart service supporting Barista drink modifiers.
/// </summary>
public class OrderCartService : ObservableObject
{
    public ObservableCollection<OrderCartItem> OrderItems { get; } = new();

    public decimal SubtotalAmount => OrderItems.Sum(item => item.LineSubtotal);
    public int TotalQuantity => OrderItems.Sum(item => item.Quantity);
    public bool HasOrderItems => OrderItems.Count > 0;

    public OrderCartService()
    {
        OrderItems.CollectionChanged += (_, args) =>
        {
            if (args.NewItems != null)
            {
                foreach (OrderCartItem item in args.NewItems)
                    item.PropertyChanged += OnLinePropertyChanged;
            }
            if (args.OldItems != null)
            {
                foreach (OrderCartItem item in args.OldItems)
                    item.PropertyChanged -= OnLinePropertyChanged;
            }
            RaiseCartChanged();
        };
    }

    private void OnLinePropertyChanged(object? sender, PropertyChangedEventArgs e) => RaiseCartChanged();

    private void RaiseCartChanged()
    {
        OnPropertyChanged(nameof(SubtotalAmount));
        OnPropertyChanged(nameof(TotalQuantity));
        OnPropertyChanged(nameof(HasOrderItems));
    }

    public string? AddProduct(
        Product product,
        string cupSize = "Regular",
        string iceLevel = "Regular Ice",
        string sweetnessLevel = "100%",
        List<string>? addOns = null,
        decimal modifierSurcharge = 0m,
        string preparationNotes = "")
    {
        if (!product.IsAvailable)
            return $"{product.ProductName} is currently marked Sold Out / Unavailable.";

        var normalizedAddOns = addOns?.OrderBy(a => a).ToList() ?? new List<string>();
        var totalUsedForProduct = OrderItems
            .Where(item => item.Product.ProductIdentifier == product.ProductIdentifier)
            .Sum(item => item.Quantity);

        if (totalUsedForProduct + 1 > product.StockQuantity)
        {
            return product.StockQuantity <= 0
                ? $"{product.ProductName} is out of stock."
                : $"Only {product.StockQuantity} {product.MeasurementUnit} of {product.ProductName} remaining.";
        }

        var existingLine = OrderItems.FirstOrDefault(item =>
            item.Product.ProductIdentifier == product.ProductIdentifier &&
            item.CupSize == cupSize &&
            item.IceLevel == iceLevel &&
            item.SweetnessLevel == sweetnessLevel &&
            item.PreparationNotes == (preparationNotes ?? string.Empty) &&
            item.AddOns.OrderBy(a => a).SequenceEqual(normalizedAddOns));

        if (existingLine == null)
        {
            OrderItems.Add(new OrderCartItem
            {
                Product = product,
                Quantity = 1,
                CupSize = cupSize,
                IceLevel = iceLevel,
                SweetnessLevel = sweetnessLevel,
                AddOns = normalizedAddOns,
                ModifierSurcharge = modifierSurcharge,
                PreparationNotes = preparationNotes?.Trim() ?? string.Empty
            });
        }
        else
        {
            existingLine.Quantity++;
        }

        return null;
    }

    public string? IncrementItem(OrderCartItem item)
    {
        var totalUsedForProduct = OrderItems
            .Where(line => line.Product.ProductIdentifier == item.Product.ProductIdentifier)
            .Sum(line => line.Quantity);

        if (totalUsedForProduct + 1 > item.Product.StockQuantity)
            return $"Only {item.Product.StockQuantity} {item.Product.MeasurementUnit} of {item.Product.ProductName} remaining.";

        item.Quantity++;
        return null;
    }

    public void DecrementItem(OrderCartItem item)
    {
        if (item.Quantity <= 1)
            OrderItems.Remove(item);
        else
            item.Quantity--;
    }

    public void RemoveItem(OrderCartItem item) => OrderItems.Remove(item);

    public void ClearCart() => OrderItems.Clear();
}
