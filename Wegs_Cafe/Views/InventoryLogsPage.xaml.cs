using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class InventoryLogsPage : ContentPage
{
    private readonly InventoryLogsViewModel _viewModel;

    public InventoryLogsPage(InventoryLogsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.LoadLogsCommand.ExecuteAsync(null);
    }
}
