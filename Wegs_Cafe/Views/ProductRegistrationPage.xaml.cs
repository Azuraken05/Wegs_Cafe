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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.LoadCatalogCommand.ExecuteAsync(null);
    }
}
