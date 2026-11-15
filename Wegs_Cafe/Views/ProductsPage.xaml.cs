using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class ProductsPage : ContentPage
{
    private readonly ProductsViewModel _vm;

    public ProductsPage(ProductsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    private void OnEditClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: ProductRow row }) _vm.EditCommand.Execute(row);
    }

    private void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: ProductRow row }) _vm.DeleteCommand.Execute(row);
    }
}
