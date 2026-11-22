using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class InventoryManagementViewModel : BaseViewModel
{
    private readonly IProductManagementService _productManagementService;
    private readonly IInventoryManagementService _inventoryManagementService;
    private readonly SessionService _sessionService;
    private readonly IDialogService _dialogService;

    private List<Product> _allProducts = new();
    private CancellationTokenSource? _searchDebounceCts;
    private bool _hasLoadedInitialData;

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

    // Stock Adjustment Modal State
    [ObservableProperty]
    private bool _isAdjustmentModalVisible;

    [ObservableProperty]
    private Product? _selectedAdjustmentProduct;

    [ObservableProperty]
    private string _selectedMovementType = InventoryMovementTypes.StockReplenishment;

    [ObservableProperty]
    private string _quantityChangeText = "20";

    [ObservableProperty]
    private string _adjustmentRemarks = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQuantityChangeError))]
    private string _quantityChangeError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAdjustmentRemarksError))]
    private string _adjustmentRemarksError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitAdjustment))]
    private bool _isAdjustmentValid;

    public bool IsAllFilterSelected => string.Equals(SelectedStockFilter, "All", StringComparison.OrdinalIgnoreCase);
    public bool IsLowStockFilterSelected => string.Equals(SelectedStockFilter, "Low Stock", StringComparison.OrdinalIgnoreCase);
    public bool IsOutOfStockFilterSelected => string.Equals(SelectedStockFilter, "Out of Stock", StringComparison.OrdinalIgnoreCase);
    public bool CanRestockInventory => _sessionService.CanRestockInventory;
    public bool HasQuantityChangeError => !string.IsNullOrWhiteSpace(QuantityChangeError);
    public bool HasAdjustmentRemarksError => !string.IsNullOrWhiteSpace(AdjustmentRemarksError);
    public bool CanSubmitAdjustment => IsAdjustmentValid && IsNotBusy;

    public IReadOnlyList<string> MovementTypeOptions { get; } = InventoryMovementTypes.EditableMovements;
    public ObservableRangeCollection<Product> FilteredProducts { get; } = new();
    public ObservableRangeCollection<InventoryLog> RecentInventoryLogs { get; } = new();

    public InventoryManagementViewModel(
        IProductManagementService productManagementService,
        IInventoryManagementService inventoryManagementService,
        SessionService sessionService,
        IDialogService dialogService)
    {
        _productManagementService = productManagementService;
        _inventoryManagementService = inventoryManagementService;
        _sessionService = sessionService;
        _dialogService = dialogService;
        PageTitle = "Inventory Management";
    }

    partial void OnSearchQueryChanged(string value)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested)
                    MainThread.BeginInvokeOnMainThread(ApplyFilters);
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }
    partial void OnSelectedStockFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedMovementTypeChanged(string value)
    {
        if (value == InventoryMovementTypes.SpoilageOrWaste || value == InventoryMovementTypes.BaristaSpillOrRemake)
        {
            if (int.TryParse(QuantityChangeText, out var parsed) && parsed > 0)
                QuantityChangeText = (-parsed).ToString();
        }
        else if (value == InventoryMovementTypes.StockReplenishment)
        {
            if (int.TryParse(QuantityChangeText, out var parsed) && parsed < 0)
                QuantityChangeText = Math.Abs(parsed).ToString();
        }
        ValidateAdjustmentLive();
    }
    partial void OnQuantityChangeTextChanged(string value) => ValidateAdjustmentLive();
    partial void OnAdjustmentRemarksChanged(string value) => ValidateAdjustmentLive();

    private void ValidateAdjustmentLive()
    {
        if (SelectedAdjustmentProduct is null)
        {
            QuantityChangeError = string.Empty;
            AdjustmentRemarksError = string.Empty;
            IsAdjustmentValid = false;
            return;
        }

        var summary = ValidationRules.ValidateInventoryAdjustment(
            SelectedAdjustmentProduct.StockQuantity,
            SelectedMovementType,
            QuantityChangeText,
            AdjustmentRemarks);

        QuantityChangeError = summary.GetError("QuantityChanged");
        AdjustmentRemarksError = summary.GetError("Remarks");
        IsAdjustmentValid = summary.IsValid;
    }

    [RelayCommand]
    public async Task LoadInventoryAsync()
    {
        if (_hasLoadedInitialData || IsBusy) return;
        _hasLoadedInitialData = true;
        await RefreshInventoryAsync();
    }

    public async Task RefreshInventoryAsync()
    {
        if (IsBusy) return;
        ClearError();

        try
        {
            IsBusy = true;
            OnPropertyChanged(nameof(CanRestockInventory));
            var products = await _productManagementService.GetProductsAsync();
            _allProducts = products.ToList();

            TotalProductCount = _allProducts.Count;
            LowStockCount = _allProducts.Count(product => product.IsLowStock);
            OutOfStockCount = _allProducts.Count(product => product.IsOutOfStock);

            ApplyFilters();

            var logs = await _inventoryManagementService.GetInventoryLogsAsync();
            RecentInventoryLogs.ReplaceAll(logs.Take(40).ToList());
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
    private void OpenAdjustmentModal(Product? product)
    {
        if (product is null) return;
        if (!CanRestockInventory)
        {
            _dialogService.ShowAlertAsync("Permission Required", "Your role is view-only for inventory. Contact the Owner or Head Barista to restock or adjust stock.");
            return;
        }

        SelectedAdjustmentProduct = product;
        SelectedMovementType = InventoryMovementTypes.StockReplenishment;
        QuantityChangeText = "20";
        AdjustmentRemarks = string.Empty;
        ValidateAdjustmentLive();
        IsAdjustmentModalVisible = true;
    }

    [RelayCommand]
    private void CloseAdjustmentModal()
    {
        IsAdjustmentModalVisible = false;
        SelectedAdjustmentProduct = null;
    }

    [RelayCommand]
    private async Task SubmitStockAdjustmentAsync()
    {
        if (SelectedAdjustmentProduct is null || IsBusy) return;
        ValidateAdjustmentLive();
        if (!IsAdjustmentValid) return;

        try
        {
            IsBusy = true;
            OnPropertyChanged(nameof(CanSubmitAdjustment));

            var delta = int.Parse(QuantityChangeText);
            await _inventoryManagementService.RecordStockMovementAsync(
                SelectedAdjustmentProduct.ProductIdentifier,
                SelectedMovementType,
                delta,
                AdjustmentRemarks.Trim());

            IsAdjustmentModalVisible = false;
            SelectedAdjustmentProduct = null;
            await RefreshInventoryAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Inventory Update Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSubmitAdjustment));
        }
    }

    private void ApplyFilters()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filtered = _allProducts.AsEnumerable();

        if (IsLowStockFilterSelected)
            filtered = filtered.Where(item => item.IsLowStock);
        else if (IsOutOfStockFilterSelected)
            filtered = filtered.Where(item => item.IsOutOfStock);

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(item =>
                item.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.StockKeepingUnit.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredProducts.ReplaceAll(filtered.OrderBy(product => product.StockQuantity).ToList());
    }
}
