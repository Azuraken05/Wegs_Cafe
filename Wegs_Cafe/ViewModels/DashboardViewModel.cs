using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;
using Wegs_Cafe.Views;

namespace Wegs_Cafe.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly IOrderService _orders;
    private readonly IProductService _products;
    private readonly IInventoryService _inventory;
    private readonly SessionService _session;
    private readonly IDialogService _dialogs;

    public ObservableCollection<Product> LowStock { get; } = new();
    public ObservableCollection<TopProductStat> TopProducts { get; } = new();
    public ObservableCollection<CategoryStat> Categories { get; } = new();
    public ObservableCollection<Order> RecentOrders { get; } = new();

    public SalesBarChartDrawable SalesChart { get; } = new();
    public PaymentBreakdownDrawable PaymentChart { get; } = new();

    /// <summary>Raised whenever chart drawables are updated so the Page can call Invalidate() on its GraphicsViews.</summary>
    public event EventHandler? ChartInvalidated;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsDay)), NotifyPropertyChangedFor(nameof(IsWeek)), NotifyPropertyChangedFor(nameof(IsMonth)), NotifyPropertyChangedFor(nameof(PeriodSubtitle))]
    private ReportPeriod _period = ReportPeriod.Day;

    [ObservableProperty] private decimal _grossSales;
    [ObservableProperty] private decimal _estimatedProfit;
    [ObservableProperty] private decimal _profitMarginPercent;
    [ObservableProperty] private int _totalOrders;
    [ObservableProperty] private int _voidedOrders;
    [ObservableProperty] private int _totalItemsSold;
    [ObservableProperty] private decimal _averageOrderValue;

    [ObservableProperty] private decimal _cash;
    [ObservableProperty] private decimal _gCash;
    [ObservableProperty] private decimal _card;
    [ObservableProperty] private int _cashCount;
    [ObservableProperty] private int _gCashCount;
    [ObservableProperty] private int _cardCount;
    [ObservableProperty] private string _cashPercentText = "0%";
    [ObservableProperty] private string _gCashPercentText = "0%";
    [ObservableProperty] private string _cardPercentText = "0%";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLowStock))] private int _lowStockCount;
    [ObservableProperty] private bool _hasTopProducts;
    [ObservableProperty] private bool _hasRecentOrders;

    public bool IsOwner => _session.CanViewProfit;
    public bool IsDay => Period == ReportPeriod.Day;
    public bool IsWeek => Period == ReportPeriod.Week;
    public bool IsMonth => Period == ReportPeriod.Month;
    public bool HasLowStock => LowStockCount > 0;
    public string LowStockTitle => LowStockCount == 1 ? "1 item is running low" : $"{LowStockCount} items are running low";

    public string PeriodSubtitle => Period switch
    {
        ReportPeriod.Day => $"Today ({DateTime.Now:MMM d, yyyy}) — Hourly Breakdown",
        ReportPeriod.Week => "This Week (Mon – Sun) — Daily Breakdown",
        _ => $"This Month ({DateTime.Now:MMMM yyyy}) — Daily Breakdown"
    };

    public DashboardViewModel(
        IOrderService orders,
        IProductService products,
        IInventoryService inventory,
        SessionService session,
        IDialogService dialogs)
    {
        _orders = orders;
        _products = products;
        _inventory = inventory;
        _session = session;
        _dialogs = dialogs;
    }

    partial void OnPeriodChanged(ReportPeriod value) => _ = LoadAsync();
    partial void OnLowStockCountChanged(int value) => OnPropertyChanged(nameof(LowStockTitle));

    /// <summary>Live updates: any sale / restock / void refreshes the dashboard.</summary>
    public void Attach() => _inventory.StockChanged += OnStockChanged;
    public void Detach() => _inventory.StockChanged -= OnStockChanged;
    private void OnStockChanged(object? sender, EventArgs e) => _ = LoadAsync();

    [RelayCommand]
    private void SetPeriod(string p)
    {
        if (Enum.TryParse<ReportPeriod>(p, true, out var parsed))
            Period = parsed;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            OnPropertyChanged(nameof(IsOwner));

            var orders = await _orders.GetOrdersAsync();
            var products = await _products.GetProductsAsync();

            var summary = SalesReport.Build(orders, products, Period, DateTime.Now);

            GrossSales = summary.GrossSales;
            EstimatedProfit = summary.EstimatedProfit;
            ProfitMarginPercent = summary.ProfitMarginPercent;
            TotalOrders = summary.TotalOrders;
            VoidedOrders = summary.VoidedOrders;
            TotalItemsSold = summary.TotalItemsSold;
            AverageOrderValue = summary.AverageOrderValue;

            Cash = summary.Cash;
            GCash = summary.GCash;
            Card = summary.Card;
            CashCount = summary.CashCount;
            GCashCount = summary.GCashCount;
            CardCount = summary.CardCount;

            CashPercentText = $"{summary.CashRatio * 100:0.#}% ({summary.CashCount})";
            GCashPercentText = $"{summary.GCashRatio * 100:0.#}% ({summary.GCashCount})";
            CardPercentText = $"{summary.CardRatio * 100:0.#}% ({summary.CardCount})";

            SalesChart.Buckets = summary.Buckets;
            PaymentChart.CashRatio = summary.CashRatio;
            PaymentChart.GCashRatio = summary.GCashRatio;
            PaymentChart.CardRatio = summary.CardRatio;

            TopProducts.Clear();
            foreach (var tp in summary.TopProducts) TopProducts.Add(tp);
            HasTopProducts = TopProducts.Count > 0;

            Categories.Clear();
            foreach (var c in summary.CategoryBreakdown) Categories.Add(c);

            RecentOrders.Clear();
            foreach (var o in summary.RecentOrders) RecentOrders.Add(o);
            HasRecentOrders = RecentOrders.Count > 0;

            var low = products.Where(p => p.IsAvailable && (p.IsLowStock || p.IsOutOfStock))
                              .OrderBy(p => p.StockQuantity)
                              .ToList();
            LowStock.Clear();
            foreach (var p in low) LowStock.Add(p);
            LowStockCount = low.Count;

            ChartInvalidated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task QuickRestockAsync(Product? p)
    {
        if (p == null) return;
        var text = await _dialogs.PromptAsync("Quick restock", $"Add stock to {p.Name}\nCurrent: {p.StockText}", "Quantity to add", numeric: true);
        if (text == null) return;
        if (!int.TryParse(text, out var qty) || qty <= 0)
        {
            await _dialogs.AlertAsync("Invalid", "Enter a whole number greater than 0.");
            return;
        }
        try
        {
            await _inventory.AdjustStockAsync(p.ProductId, InventoryChangeTypes.Restock, qty);
        }
        catch (Exception ex) when (ex is ValidationException or UnauthorizedAccessException)
        {
            await _dialogs.AlertAsync("Error", ex.Message);
        }
    }
}
