using CommunityToolkit.Mvvm.ComponentModel;

namespace Wegs_Cafe.Models;

/// <summary>Local-only cart line (never written to Firebase directly; becomes an OrderItem).</summary>
public partial class CartItem : ObservableObject
{
    public Product Product { get; init; } = new();

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Subtotal))]
    private int _qty = 1;

    [ObservableProperty]
    private string _notes = string.Empty;

    public decimal Subtotal => Product.Price * Qty;
}
