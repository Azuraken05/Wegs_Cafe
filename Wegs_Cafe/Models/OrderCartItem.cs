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
    [NotifyPropertyChangedFor(nameof(LineSubtotal))]
    [NotifyPropertyChangedFor(nameof(EffectiveUnitPrice))]
    [NotifyPropertyChangedFor(nameof(ModifierSummary))]
    private string _cupSize = "Regular";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierSummary))]
    private string _iceLevel = "Regular Ice";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierSummary))]
    private string _sweetnessLevel = "100%";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierSummary))]
    private List<string> _addOns = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineSubtotal))]
    [NotifyPropertyChangedFor(nameof(EffectiveUnitPrice))]
    private decimal _modifierSurcharge;

    [ObservableProperty]
    private string _preparationNotes = string.Empty;

    public decimal EffectiveUnitPrice => Product.SellingPrice + ModifierSurcharge;
    public decimal LineSubtotal => EffectiveUnitPrice * Quantity;

    public string ModifierSummary
    {
        get
        {
            if (!Product.AllowDrinkModifiers) return string.Empty;
            var parts = new List<string> { CupSize, IceLevel, $"{SweetnessLevel} Sweet" };
            if (AddOns.Count > 0)
                parts.AddRange(AddOns);
            return string.Join(" · ", parts);
        }
    }
}
