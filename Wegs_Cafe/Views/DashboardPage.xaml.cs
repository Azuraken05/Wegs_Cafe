using Wegs_Cafe.Models;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _vm;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
        _vm.ChartInvalidated += OnChartInvalidated;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.Attach();
        await _vm.LoadAsync();
        InvalidateCharts();
    }

    protected override void OnDisappearing()
    {
        _vm.Detach();
        base.OnDisappearing();
    }

    private void OnChartInvalidated(object? sender, EventArgs e) => InvalidateCharts();

    private void InvalidateCharts()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SalesChartView?.Invalidate();
            PaymentChartView?.Invalidate();
        });
    }

    private void OnQuickRestockClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is Product p)
            _vm.QuickRestockCommand.Execute(p);
    }
}
