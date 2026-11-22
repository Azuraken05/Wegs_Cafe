using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class InventoryManagementViewModel : BaseViewModel
{
    private readonly IProductManagementService _productManagementService;
    private readonly IInventoryManagementService _inventoryManagementService;
    private readonly IDialogService _dialogService;

    private List<Product> _allProducts = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAllFilterSelected))]
    [NotifyPropertyChangedFor(nameof(IsLowStockFilterSelected))]
    [NotifyPropertyChangedFor(nameof(IsOutOfStockFilterSelected))]
    private string _selectedStockFilter = "All";

    [ObservableProperty]
    private int _totalProductCount;

    [ObservableProperty]
    private int _lowStockCount;

    [ObservableProperty]
    private int _outOfStockCount;

    [ObservableProperty]
    private decimal _totalStockValuation;

    public bool IsAllFilterSelected => string.Equals(SelectedStockFilter, "All", StringComparison.OrdinalIgnoreCase);
    public bool IsLowStockFilterSelected => string.Equals(SelectedStockFilter, "Low Stock", StringComparison.OrdinalIgnoreCase);
    public bool IsOutOfStockFilterSelected => string.Equals(SelectedStockFilter, "Out of Stock", StringComparison.OrdinalIgnoreCase);

    public ObservableCollection<Product> FilteredProducts { get; } = new();
    public ObservableCollection<InventoryLog> RecentInventoryLogs { get; } = new();

    public InventoryManagementViewModel(
        IProductManagementService productManagementService,
        IInventoryManagementService inventoryManagementService,
        IDialogService dialogService)
    {
        _productManagementService = productManagementService;
        _inventoryManagementService = inventoryManagementService;
        _dialogService = dialogService;
        PageTitle = "Inventory Management & Stock Ledger";
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedStockFilterChanged(string value) => ApplyFilters();

    [RelayCommand]
    public async Task LoadInventoryAsync()
    {
        if (IsBusy) return;
        ClearError();

        try
        {
            IsBusy = true;
            var products = await _productManagementService.GetProductsAsync();
            _allProducts = products.ToList();

            TotalProductCount = _allProducts.Count;
            LowStockCount = _allProducts.Count(product => product.IsLowStock);
            OutOfStockCount = _allProducts.Count(product => product.IsOutOfStock);
            TotalStockValuation = _allProducts.Sum(product => product.CostPrice * product.StockQuantity);

            ApplyFilters();

            var logs = await _inventoryManagementService.GetInventoryLogsAsync();
            RecentInventoryLogs.Clear();
            foreach (var log in logs.Take(40))
                RecentInventoryLogs.Add(log);
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
    private void SelectStockFilter(string filterName)
    {
        SelectedStockFilter = string.IsNullOrWhiteSpace(filterName) ? "All" : filterName;
    }

    [RelayCommand]
    private async Task ReplenishStockAsync(Product? product)
    {
        if (product is null) return;

        var input = await _dialogService.ShowPromptAsync(
            "Stock Replenishment",
            $"Enter quantity of {product.MeasurementUnit} to add for {product.ProductName} (Current: {product.StockQuantity}):",
            "20",
            numeric: true,
            initialValue: "20");

        if (string.IsNullOrWhiteSpace(input)) return;

        if (!int.TryParse(input.Trim(), out var quantity) || quantity <= 0)
        {
            await _dialogService.ShowAlertAsync("Invalid Quantity", "Please enter a positive whole number.");
            return;
        }

        await ExecuteMovementAsync(product, InventoryMovementTypes.StockReplenishment, quantity);
    }

    [RelayCommand]
    private async Task RecordSpoilageOrWasteAsync(Product? product)
    {
        if (product is null) return;

        var input = await _dialogService.ShowPromptAsync(
            "Spoilage or Waste Deduction",
            $"Enter quantity of {product.MeasurementUnit} to deduct for {product.ProductName} (Current: {product.StockQuantity}):",
            "1",
            numeric: true,
            initialValue: "1");

        if (string.IsNullOrWhiteSpace(input)) return;

        if (!int.TryParse(input.Trim(), out var quantity) || quantity <= 0)
        {
            await _dialogService.ShowAlertAsync("Invalid Quantity", "Please enter a positive whole number.");
            return;
        }

        await ExecuteMovementAsync(product, InventoryMovementTypes.SpoilageOrWaste, quantity);
    }

    [RelayCommand]
    private async Task SetManualStockCountAsync(Product? product)
    {
        if (product is null) return;

        var input = await _dialogService.ShowPromptAsync(
            "Manual Physical Stock Count",
            $"Enter exact physical count of {product.MeasurementUnit} for {product.ProductName} (Current: {product.StockQuantity}):",
            product.StockQuantity.ToString(),
            numeric: true,
            initialValue: product.StockQuantity.ToString());

        if (string.IsNullOrWhiteSpace(input)) return;

        if (!int.TryParse(input.Trim(), out var newStockCount) || newStockCount < 0)
        {
            await _dialogService.ShowAlertAsync("Invalid Count", "Please enter a non-negative whole number.");
            return;
        }

        var delta = newStockCount - product.StockQuantity;
        if (delta == 0) return;

        await ExecuteMovementAsync(product, InventoryMovementTypes.ManualAdjustment, delta);
    }

    private async Task ExecuteMovementAsync(Product product, string movementType, int quantityChanged)
    {
        try
        {
            await _inventoryManagementService.RecordStockMovementAsync(
                product.ProductIdentifier,
                movementType,
                quantityChanged);

            await LoadInventoryAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Inventory Update Error", ex.Message);
        }
    }

    private void ApplyFilters()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filtered = _allProducts.AsEnumerable();

        if (IsLowStockFilterSelected)
        {
            filtered = filtered.Where(item => item.IsLowStock);
        }
        else if (IsOutOfStockFilterSelected)
        {
            filtered = filtered.Where(item => item.IsOutOfStock);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(item =>
                item.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.StockKeepingUnit.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredProducts.Clear();
        foreach (var item in filtered.OrderBy(product => product.StockQuantity))
            FilteredProducts.Add(item);
    }
}
