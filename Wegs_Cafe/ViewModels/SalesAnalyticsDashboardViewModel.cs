using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;
using Wegs_Cafe.Views;

namespace Wegs_Cafe.ViewModels;

public partial class SalesAnalyticsDashboardViewModel : BaseViewModel
{
    private readonly IOrderTransactionService _orderTransactionService;
    private readonly IProductManagementService _productManagementService;
    private readonly IInventoryManagementService _inventoryManagementService;
    private readonly IReportExportService _reportExportService;
    private readonly SessionService _sessionService;
    private readonly IDialogService _dialogService;

    private IReadOnlyList<Order> _cachedOrders = Array.Empty<Order>();
    private IReadOnlyList<Product> _cachedProducts = Array.Empty<Product>();
    private IReadOnlyList<Order> _filteredOrders = Array.Empty<Order>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDayTimeframe))]
    [NotifyPropertyChangedFor(nameof(IsWeekTimeframe))]
    [NotifyPropertyChangedFor(nameof(IsMonthTimeframe))]
    private SalesAnalyticsTimeframe _selectedTimeframe = SalesAnalyticsTimeframe.Day;

    [ObservableProperty]
    private string _timeframeHeaderTitle = "Today — Hourly Sales";

    [ObservableProperty]
    private decimal _grossSalesRevenue;

    [ObservableProperty]
    private decimal _estimatedGrossProfit;

    [ObservableProperty]
    private string _profitMarginText = "0.0% Margin";

    [ObservableProperty]
    private int _completedOrderCount;

    [ObservableProperty]
    private int _voidedOrderCount;

    [ObservableProperty]
    private decimal _averageOrderTicket;

    [ObservableProperty]
    private int _totalItemsSold;

    [ObservableProperty]
    private decimal _cashSalesRevenue;

    [ObservableProperty]
    private decimal _gcashSalesRevenue;

    [ObservableProperty]
    private decimal _creditCardSalesRevenue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInventoryAlerts))]
    private int _inventoryAlertCount;

    [ObservableProperty]
    private int _lowStockProductCount;

    [ObservableProperty]
    private int _outOfStockProductCount;

    [ObservableProperty]
    private IDrawable _salesTrendChartDrawable = new SalesTrendBarChartDrawable(Array.Empty<SalesChartBucket>());

    [ObservableProperty]
    private IDrawable _paymentMethodChartDrawable = new PaymentMethodBreakdownDrawable(0m, 0m, 0m);

    public bool IsDayTimeframe => SelectedTimeframe == SalesAnalyticsTimeframe.Day;
    public bool IsWeekTimeframe => SelectedTimeframe == SalesAnalyticsTimeframe.Week;
    public bool IsMonthTimeframe => SelectedTimeframe == SalesAnalyticsTimeframe.Month;
    public bool HasInventoryAlerts => InventoryAlertCount > 0;
    public bool IsOwnerAccount => _sessionService.IsOwner;
    public string ActiveUserGreeting => _sessionService.ActiveUser != null
        ? $"Signed in as {_sessionService.ActiveUser.FullName} ({_sessionService.ActiveUser.Role.ToUpperInvariant()})"
        : "Signed in";

    public ObservableCollection<Product> LowStockAlertProducts { get; } = new();
    public ObservableCollection<TopSellingProductRank> TopSellingProducts { get; } = new();
    public ObservableCollection<CategoryPerformanceSummary> CategoryBreakdown { get; } = new();
    public ObservableCollection<Order> RecentOrders { get; } = new();

    public event EventHandler? AnalyticsUpdated;

    public SalesAnalyticsDashboardViewModel(
        IOrderTransactionService orderTransactionService,
        IProductManagementService productManagementService,
        IInventoryManagementService inventoryManagementService,
        IReportExportService reportExportService,
        SessionService sessionService,
        IDialogService dialogService)
    {
        _orderTransactionService = orderTransactionService;
        _productManagementService = productManagementService;
        _inventoryManagementService = inventoryManagementService;
        _reportExportService = reportExportService;
        _sessionService = sessionService;
        _dialogService = dialogService;
        PageTitle = "Sales & Analytics Dashboard";

        _inventoryManagementService.InventoryChanged += async (_, _) => await LoadDashboardAnalyticsAsync();
        _sessionService.ActiveUserChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsOwnerAccount));
            OnPropertyChanged(nameof(ActiveUserGreeting));
        };
    }

    [RelayCommand]
    public async Task LoadDashboardAnalyticsAsync()
    {
        if (IsBusy) return;
        ClearError();

        try
        {
            IsBusy = true;
            OnPropertyChanged(nameof(IsOwnerAccount));
            OnPropertyChanged(nameof(ActiveUserGreeting));

            _cachedOrders = await _orderTransactionService.GetOrdersAsync();
            _cachedProducts = await _productManagementService.GetProductsAsync();

            RecalculateSnapshot();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectTimeframe(string timeframeKey)
    {
        SelectedTimeframe = timeframeKey?.ToLowerInvariant() switch
        {
            "week" => SalesAnalyticsTimeframe.Week,
            "month" => SalesAnalyticsTimeframe.Month,
            _ => SalesAnalyticsTimeframe.Day
        };

        RecalculateSnapshot();
    }

    [RelayCommand]
    private async Task RestockItemAsync(Product? product)
    {
        if (product is null) return;

        var input = await _dialogService.ShowPromptAsync(
            "Restock Product",
            $"Enter quantity of {product.MeasurementUnit} to add for {product.ProductName} (Current Stock: {product.StockQuantity}):",
            "25",
            numeric: true,
            initialValue: "25");

        if (string.IsNullOrWhiteSpace(input)) return;

        if (!int.TryParse(input.Trim(), out var addQuantity) || addQuantity <= 0)
        {
            await _dialogService.ShowAlertAsync("Invalid Quantity", "Please enter a positive whole number.");
            return;
        }

        try
        {
            await _inventoryManagementService.RecordStockMovementAsync(
                product.ProductIdentifier,
                InventoryMovementTypes.StockReplenishment,
                addQuantity);

            await LoadDashboardAnalyticsAsync();
            await _dialogService.ShowAlertAsync(
                "Stock Replenished",
                $"Added +{addQuantity} {product.MeasurementUnit} to {product.ProductName}.");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Restock Error", ex.Message);
        }
    }

    [RelayCommand]
    private async Task OpenInventoryManagementAsync()
    {
        await Shell.Current.GoToAsync($"//{nameof(InventoryManagementPage)}");
    }

    [RelayCommand]
    private async Task OpenPointOfSaleAsync()
    {
        await Shell.Current.GoToAsync($"//{nameof(PointOfSalePage)}");
    }

    [RelayCommand]
    private async Task ExportSalesReportExcelAsync()
    {
        if (!IsOwnerAccount)
        {
            await _dialogService.ShowAlertAsync("Owner Access Required", "Only the Owner account can export sales reports.");
            return;
        }

        try
        {
            IsBusy = true;
            var filePath = await _reportExportService.ExportSalesReportToExcelAsync(
                _filteredOrders,
                _cachedProducts,
                TimeframeHeaderTitle);

            await _dialogService.ShowAlertAsync(
                "Excel Report Exported",
                $"Sales & Inventory Valuation workbook saved to:\n{filePath}");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Export Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportSalesReportCsvAsync()
    {
        if (!IsOwnerAccount)
        {
            await _dialogService.ShowAlertAsync("Owner Access Required", "Only the Owner account can export sales reports.");
            return;
        }

        try
        {
            IsBusy = true;
            var filePath = await _reportExportService.ExportSalesReportToCsvAsync(
                _filteredOrders,
                _cachedProducts,
                TimeframeHeaderTitle);

            await _dialogService.ShowAlertAsync(
                "CSV Report Exported",
                $"CSV report saved to:\n{filePath}");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Export Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RecalculateSnapshot()
    {
        var snapshot = SalesAnalyticsCalculator.ComputeSnapshot(_cachedOrders, _cachedProducts, SelectedTimeframe);
        _filteredOrders = snapshot.FilteredOrders;

        TimeframeHeaderTitle = snapshot.TimeframeTitle;
        GrossSalesRevenue = snapshot.GrossSalesRevenue;
        EstimatedGrossProfit = snapshot.EstimatedGrossProfit;
        ProfitMarginText = $"{snapshot.ProfitMarginRatio * 100:0.0}% Gross Margin";
        CompletedOrderCount = snapshot.CompletedOrderCount;
        VoidedOrderCount = snapshot.VoidedOrderCount;
        AverageOrderTicket = snapshot.AverageOrderTicket;
        TotalItemsSold = snapshot.TotalItemsSold;
        CashSalesRevenue = snapshot.CashSalesRevenue;
        GcashSalesRevenue = snapshot.GCashSalesRevenue;
        CreditCardSalesRevenue = snapshot.CreditCardSalesRevenue;

        SalesTrendChartDrawable = new SalesTrendBarChartDrawable(snapshot.ChartBuckets);
        PaymentMethodChartDrawable = new PaymentMethodBreakdownDrawable(
            snapshot.CashSalesRevenue,
            snapshot.GCashSalesRevenue,
            snapshot.CreditCardSalesRevenue);

        TopSellingProducts.Clear();
        foreach (var rank in snapshot.TopSellingProducts)
            TopSellingProducts.Add(rank);

        CategoryBreakdown.Clear();
        foreach (var category in snapshot.CategoryBreakdown)
            CategoryBreakdown.Add(category);

        RecentOrders.Clear();
        foreach (var order in snapshot.RecentOrders)
            RecentOrders.Add(order);

        var alertProducts = _cachedProducts
            .Where(product => product.IsOutOfStock || product.IsLowStock)
            .OrderBy(product => product.StockQuantity)
            .ToList();

        OutOfStockProductCount = _cachedProducts.Count(product => product.IsOutOfStock);
        LowStockProductCount = _cachedProducts.Count(product => product.IsLowStock);
        InventoryAlertCount = alertProducts.Count;

        LowStockAlertProducts.Clear();
        foreach (var item in alertProducts)
            LowStockAlertProducts.Add(item);

        AnalyticsUpdated?.Invoke(this, EventArgs.Empty);
    }
}
