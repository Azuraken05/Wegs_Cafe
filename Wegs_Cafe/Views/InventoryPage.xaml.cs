using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class InventoryPage : ContentPage
{
    private readonly InventoryViewModel _vm;

    public InventoryPage(InventoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    private void OnRestockClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: ProductRow row }) _vm.RestockCommand.Execute(row);
    }

    private void OnAdjustClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: ProductRow row }) _vm.AdjustCommand.Execute(row);
    }
}
