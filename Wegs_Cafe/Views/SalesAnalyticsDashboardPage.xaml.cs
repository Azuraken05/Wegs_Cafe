using Wegs_Cafe.Helpers;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class SalesAnalyticsDashboardPage : ContentPage
{
    private readonly SalesAnalyticsDashboardViewModel _viewModel;

    public SalesAnalyticsDashboardPage(SalesAnalyticsDashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.AnalyticsUpdated += OnAnalyticsUpdated;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDashboardAnalyticsCommand.ExecuteAsync(null);
        await AnimationHelper.AnimateEntranceAsync(DashboardContentContainer);

        if (_viewModel.HasInventoryAlerts)
        {
            await AnimationHelper.AnimateAlertBannerAsync(LowStockNotificationBanner);
        }
    }

    private void OnAnalyticsUpdated(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SalesTrendChartView?.Invalidate();
            PaymentSplitChartView?.Invalidate();
        });
    }
}
