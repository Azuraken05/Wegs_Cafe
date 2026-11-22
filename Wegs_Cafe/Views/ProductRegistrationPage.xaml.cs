using Wegs_Cafe.Helpers;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class ProductRegistrationPage : ContentPage
{
    private readonly ProductRegistrationViewModel _viewModel;
    private bool _hasAnimated;

    public ProductRegistrationPage(ProductRegistrationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCatalogCommand.ExecuteAsync(null);
        if (!_hasAnimated)
        {
            _hasAnimated = true;
            await AnimationHelper.AnimateEntranceAsync(ProductPageContainer, 150);
        }
    }
}
