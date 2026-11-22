using CommunityToolkit.Mvvm.ComponentModel;

namespace Wegs_Cafe.Models;

/// <summary>In-memory Point of Sale cart line item (converted into an OrderItem on checkout).</summary>
public partial class OrderCartItem : ObservableObject
{
    public Product Product { get; init; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineSubtotal))]
    private int _quantity = 1;

    [ObservableProperty]
    private string _preparationNotes = string.Empty;

    public decimal LineSubtotal => Product.SellingPrice * Quantity;
}
