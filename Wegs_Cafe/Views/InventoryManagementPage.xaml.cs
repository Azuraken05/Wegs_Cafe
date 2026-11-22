using Wegs_Cafe.Helpers;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class InventoryManagementPage : ContentPage
{
    private readonly InventoryManagementViewModel _viewModel;

    public InventoryManagementPage(InventoryManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadInventoryCommand.ExecuteAsync(null);
        await AnimationHelper.AnimateEntranceAsync(InventoryContainer);
    }
}
