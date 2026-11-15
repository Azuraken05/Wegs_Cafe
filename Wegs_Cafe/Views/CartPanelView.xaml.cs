using Wegs_Cafe.Models;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class CartPanelView : ContentView
{
    public CartPanelView() => InitializeComponent();

    private PosViewModel? Vm => BindingContext as PosViewModel;
    private static CartItem? Line(object? sender) => (sender as BindableObject)?.BindingContext as CartItem;

    private void OnIncrement(object? sender, EventArgs e) { if (Line(sender) is { } l) Vm?.IncrementCommand.Execute(l); }
    private void OnDecrement(object? sender, EventArgs e) { if (Line(sender) is { } l) Vm?.DecrementCommand.Execute(l); }
    private void OnRemove(object? sender, EventArgs e) { if (Line(sender) is { } l) Vm?.RemoveCommand.Execute(l); }
}
