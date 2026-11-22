using Wegs_Cafe.Helpers;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class InventoryManagementPage : ContentPage
{
    private readonly InventoryManagementViewModel _viewModel;
    private bool _hasAnimated;

    public InventoryManagementPage(InventoryManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.LoadInventoryCommand.ExecuteAsync(null);
    }
}
