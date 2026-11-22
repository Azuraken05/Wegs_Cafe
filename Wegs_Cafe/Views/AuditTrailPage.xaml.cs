using Wegs_Cafe.Helpers;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class AuditTrailPage : ContentPage
{
    private readonly AuditTrailViewModel _viewModel;

    public AuditTrailPage(AuditTrailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAuditLogsCommand.ExecuteAsync(null);
        await AnimationHelper.AnimateEntranceAsync(AuditTrailContainer);
    }
}
