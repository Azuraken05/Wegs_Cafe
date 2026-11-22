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
        _viewModel.DrinkModifierModalRequested += OnDrinkModifierModalRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadPointOfSaleDataCommand.ExecuteAsync(null);
        await AnimationHelper.AnimateEntranceAsync(PointOfSaleContainer);
    }

    private async void OnDrinkModifierModalRequested(object? sender, EventArgs e)
    {
        DrinkModifierBackdrop.Opacity = 0;
        DrinkModifierSheet.TranslationY = 60;
        await Task.WhenAll(
            DrinkModifierBackdrop.FadeToAsync(1, 200, Easing.CubicOut),
            DrinkModifierSheet.TranslateToAsync(0, 0, 220, Easing.CubicOut));
    }

    private async void OnProductCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement element && element.BindingContext is Product product)
        {
            await AnimationHelper.AnimatePressAsync(element);
            await _viewModel.HandleProductTileTappedCommand.ExecuteAsync(product);
        }
    }
}
