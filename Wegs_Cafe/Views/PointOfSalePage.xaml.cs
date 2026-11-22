using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class PointOfSalePage : ContentPage
{
    private readonly PointOfSaleViewModel _viewModel;

    public PointOfSalePage(PointOfSaleViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadPointOfSaleDataCommand.ExecuteAsync(null);
        await AnimationHelper.AnimateEntranceAsync(PointOfSaleContainer);
    }

    private async void OnProductCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement element && element.BindingContext is Product product)
        {
            await AnimationHelper.AnimatePressAsync(element);
            await _viewModel.AddProductToCartCommand.ExecuteAsync(product);
        }
    }
}
