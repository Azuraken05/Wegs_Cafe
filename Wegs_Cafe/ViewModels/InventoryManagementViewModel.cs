using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class InventoryManagementViewModel : BaseViewModel
{
    private readonly IInventoryManagementService _inventoryManagementService;
    private readonly SessionService _sessionService;
    private readonly IDialogService _dialogService;

    private List<Ingredient> _allIngredients = new();
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
    private string _selectedCategory = "All";

    [ObservableProperty]
    private int _totalIngredientCount;

    [ObservableProperty]
    private int _lowStockCount;

    [ObservableProperty]
    private int _outOfStockCount;

    // Stock Adjustment / Restock Modal State
    [ObservableProperty]
    private bool _isAdjustmentModalVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulkUnitLabel))]
    [NotifyPropertyChangedFor(nameof(RestockPreviewText))]
    private Ingredient? _selectedAdjustmentIngredient;

    [ObservableProperty]
    private string _selectedMovementType = InventoryMovementTypes.StockReplenishment;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulkUnitLabel))]
    [NotifyPropertyChangedFor(nameof(RestockPreviewText))]
    private bool _isBulkMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RestockPreviewText))]
    private string _quantityChangeText = "1";

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

    // Ingredient Registration Drawer / Modal State
    [ObservableProperty]
    private bool _isRegistrationModalVisible;

    [ObservableProperty]
    private string _newIngredientName = string.Empty;

    [ObservableProperty]
    private string _newIngredientSku = string.Empty;

    [ObservableProperty]
    private string _newIngredientCategory = IngredientCategories.DairyAndMilk;

    [ObservableProperty]
    private string _newMeasurementUnit = MeasurementUnits.Milliliters;

    [ObservableProperty]
    private string _newCurrentStockText = "1000";

    [ObservableProperty]
    private string _newLowStockThresholdText = "200";

    [ObservableProperty]
    private string _newUnitCostText = "0.08";

    [ObservableProperty]
    private string _registrationError = string.Empty;

    public bool IsAllFilterSelected => string.Equals(SelectedStockFilter, "All", StringComparison.OrdinalIgnoreCase);
    public bool IsLowStockFilterSelected => string.Equals(SelectedStockFilter, "Low Stock", StringComparison.OrdinalIgnoreCase);
    public bool IsOutOfStockFilterSelected => string.Equals(SelectedStockFilter, "Out of Stock", StringComparison.OrdinalIgnoreCase);
    public bool CanRestockInventory => _sessionService.CanRestockInventory;
    public bool CanRegisterIngredients => _sessionService.CanRegisterProducts || _sessionService.CanRestockInventory;
    public bool HasQuantityChangeError => !string.IsNullOrWhiteSpace(QuantityChangeError);
    public bool HasAdjustmentRemarksError => !string.IsNullOrWhiteSpace(AdjustmentRemarksError);
    public bool HasRegistrationError => !string.IsNullOrWhiteSpace(RegistrationError);
    public bool CanSubmitAdjustment => IsAdjustmentValid && IsNotBusy;

    public IReadOnlyList<string> MovementTypeOptions { get; } = InventoryMovementTypes.EditableMovements;
    public IReadOnlyList<string> CategoryFilterOptions { get; } = IngredientCategories.AllWithFilter;
    public IReadOnlyList<string> CategoryOptions { get; } = IngredientCategories.All;
    public IReadOnlyList<string> MeasurementUnitOptions { get; } = MeasurementUnits.All;

    public ObservableRangeCollection<Ingredient> FilteredIngredients { get; } = new();

    public string BulkUnitLabel
    {
        get
        {
            if (SelectedAdjustmentIngredient == null) return "Bulk Pack";
            return SelectedAdjustmentIngredient.MeasurementUnit.ToLowerInvariant() switch
            {
                "ml" => "Liters (1 L = 1,000 ml)",
                "g" => "Kilograms (1 kg = 1,000 g)",
                "pcs" => "Box (1 Box = 50 pcs)",
                _ => "Bulk Pack"
            };
        }
    }

    public decimal CalculatedBaseQuantityDelta
    {
        get
        {
            if (!decimal.TryParse(QuantityChangeText?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var input) || input == 0m)
                return 0m;

            if (!IsBulkMode || SelectedAdjustmentIngredient == null)
                return input;

            var multiplier = SelectedAdjustmentIngredient.MeasurementUnit.ToLowerInvariant() switch
            {
                "ml" => 1000m,
                "g" => 1000m,
                "pcs" => 50m,
                _ => 1m
            };

            return input * multiplier;
        }
    }

    public string RestockPreviewText
    {
        get
        {
            if (SelectedAdjustmentIngredient == null) return string.Empty;
            var delta = CalculatedBaseQuantityDelta;
            if (delta == 0m) return "Enter quantity";

            var finalStock = SelectedAdjustmentIngredient.CurrentStockQuantity + delta;
            var sign = delta > 0m ? "+" : "";
            return $"{sign}{delta:N0} {SelectedAdjustmentIngredient.MeasurementUnit} &rarr; Final: {finalStock:N0} {SelectedAdjustmentIngredient.MeasurementUnit}";
        }
    }

    public InventoryManagementViewModel(
        IInventoryManagementService inventoryManagementService,
        SessionService sessionService,
        IDialogService dialogService)
    {
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
                await Task.Delay(250, token);
                if (!token.IsCancellationRequested)
                    MainThread.BeginInvokeOnMainThread(ApplyFilters);
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    partial void OnSelectedStockFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();

    partial void OnSelectedMovementTypeChanged(string value)
    {
        if (value == InventoryMovementTypes.SpoilageOrWaste || value == InventoryMovementTypes.BaristaSpillOrRemake)
        {
            if (decimal.TryParse(QuantityChangeText, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) && parsed > 0m)
                QuantityChangeText = (-parsed).ToString(CultureInfo.InvariantCulture);
        }
        else if (value == InventoryMovementTypes.StockReplenishment)
        {
            if (decimal.TryParse(QuantityChangeText, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) && parsed < 0m)
                QuantityChangeText = Math.Abs(parsed).ToString(CultureInfo.InvariantCulture);
        }
        ValidateAdjustmentLive();
    }

    partial void OnQuantityChangeTextChanged(string value) => ValidateAdjustmentLive();
    partial void OnIsBulkModeChanged(bool value) => ValidateAdjustmentLive();
    partial void OnAdjustmentRemarksChanged(string value) => ValidateAdjustmentLive();

    private void ValidateAdjustmentLive()
    {
        if (SelectedAdjustmentIngredient is null)
        {
            QuantityChangeError = string.Empty;
            AdjustmentRemarksError = string.Empty;
            IsAdjustmentValid = false;
            return;
        }

        var delta = CalculatedBaseQuantityDelta;
        var summary = ValidationRules.ValidateInventoryAdjustment(
            SelectedAdjustmentIngredient.CurrentStockQuantity,
            SelectedMovementType,
            delta.ToString(CultureInfo.InvariantCulture),
            AdjustmentRemarks);

        QuantityChangeError = summary.GetError("QuantityChanged");
        AdjustmentRemarksError = summary.GetError("Remarks");
        IsAdjustmentValid = summary.IsValid;
        OnPropertyChanged(nameof(BulkUnitLabel));
        OnPropertyChanged(nameof(RestockPreviewText));
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
            OnPropertyChanged(nameof(CanRegisterIngredients));

            var ingredients = await _inventoryManagementService.GetIngredientsAsync();
            _allIngredients = ingredients.ToList();

            TotalIngredientCount = _allIngredients.Count;
            LowStockCount = _allIngredients.Count(ing => ing.IsLowStock);
            OutOfStockCount = _allIngredients.Count(ing => ing.IsOutOfStock);

            ApplyFilters();
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
    private void SelectCategoryFilter(string category)
    {
        SelectedCategory = string.IsNullOrWhiteSpace(category) ? "All" : category;
    }

    [RelayCommand]
    private void OpenAdjustmentModal(Ingredient? ingredient)
    {
        if (ingredient is null) return;
        if (!CanRestockInventory)
        {
            _dialogService.ShowAlertAsync("Permission Required", "Your role is view-only for inventory. Contact the Owner or Manager to restock.");
            return;
        }

        SelectedAdjustmentIngredient = ingredient;
        SelectedMovementType = InventoryMovementTypes.StockReplenishment;
        IsBulkMode = false;
        QuantityChangeText = ingredient.MeasurementUnit == MeasurementUnits.Pieces ? "50" : "500";
        AdjustmentRemarks = string.Empty;
        ValidateAdjustmentLive();
        IsAdjustmentModalVisible = true;
    }

    [RelayCommand]
    private void OpenSpoilageModal(Ingredient? ingredient)
    {
        if (ingredient is null) return;
        if (!CanRestockInventory)
        {
            _dialogService.ShowAlertAsync("Permission Required", "Your role is view-only for inventory.");
            return;
        }

        SelectedAdjustmentIngredient = ingredient;
        SelectedMovementType = InventoryMovementTypes.SpoilageOrWaste;
        IsBulkMode = false;
        QuantityChangeText = ingredient.MeasurementUnit == MeasurementUnits.Pieces ? "-5" : "-100";
        AdjustmentRemarks = "Expired / Spilled";
        ValidateAdjustmentLive();
        IsAdjustmentModalVisible = true;
    }

    [RelayCommand]
    private void CloseAdjustmentModal()
    {
        IsAdjustmentModalVisible = false;
        SelectedAdjustmentIngredient = null;
    }

    [RelayCommand]
    private async Task SubmitStockAdjustmentAsync()
    {
        if (SelectedAdjustmentIngredient is null || IsBusy) return;
        ValidateAdjustmentLive();
        if (!IsAdjustmentValid) return;

        try
        {
            IsBusy = true;
            OnPropertyChanged(nameof(CanSubmitAdjustment));

            var delta = CalculatedBaseQuantityDelta;
            await _inventoryManagementService.AdjustIngredientStockAsync(
                SelectedAdjustmentIngredient.IngredientIdentifier,
                delta,
                SelectedMovementType,
                AdjustmentRemarks.Trim());

            IsAdjustmentModalVisible = false;
            SelectedAdjustmentIngredient = null;
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

    [RelayCommand]
    private void OpenRegistrationModal()
    {
        if (!CanRegisterIngredients)
        {
            _dialogService.ShowAlertAsync("Permission Required", "Your role does not permit registering new ingredients.");
            return;
        }

        NewIngredientName = string.Empty;
        NewIngredientSku = $"ING-{DateTime.Now:fff}";
        NewIngredientCategory = IngredientCategories.DairyAndMilk;
        NewMeasurementUnit = MeasurementUnits.Milliliters;
        NewCurrentStockText = "1000";
        NewLowStockThresholdText = "200";
        NewUnitCostText = "0.08";
        RegistrationError = string.Empty;
        IsRegistrationModalVisible = true;
    }

    [RelayCommand]
    private void CloseRegistrationModal()
    {
        IsRegistrationModalVisible = false;
    }

    [RelayCommand]
    private async Task SubmitNewIngredientAsync()
    {
        if (IsBusy) return;

        var validation = ValidationRules.ValidateIngredientRegistration(
            NewIngredientName,
            NewIngredientSku,
            NewIngredientCategory,
            NewMeasurementUnit,
            NewCurrentStockText,
            NewLowStockThresholdText,
            NewUnitCostText,
            _allIngredients.Select(i => i.StockKeepingUnit));

        if (!validation.IsValid)
        {
            RegistrationError = validation.FirstErrorOrEmpty;
            return;
        }

        try
        {
            IsBusy = true;
            RegistrationError = string.Empty;

            var newIngredient = new Ingredient
            {
                IngredientIdentifier = $"ing-{Guid.NewGuid():N}",
                StockKeepingUnit = NewIngredientSku.Trim().ToUpperInvariant(),
                IngredientName = NewIngredientName.Trim(),
                Category = NewIngredientCategory,
                MeasurementUnit = NewMeasurementUnit,
                CurrentStockQuantity = decimal.Parse(NewCurrentStockText.Trim(), CultureInfo.InvariantCulture),
                LowStockThreshold = decimal.Parse(NewLowStockThresholdText.Trim(), CultureInfo.InvariantCulture),
                UnitCost = decimal.Parse(NewUnitCostText.Trim(), CultureInfo.InvariantCulture),
                UpdatedAt = IsoTimestamp.Now()
            };

            await _inventoryManagementService.SaveIngredientAsync(newIngredient);

            IsRegistrationModalVisible = false;
            await RefreshInventoryAsync();
            await _dialogService.ShowAlertAsync("Success", $"Ingredient '{newIngredient.IngredientName}' registered successfully.");
        }
        catch (Exception ex)
        {
            RegistrationError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilters()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filtered = _allIngredients.AsEnumerable();

        if (IsLowStockFilterSelected)
            filtered = filtered.Where(item => item.IsLowStock);
        else if (IsOutOfStockFilterSelected)
            filtered = filtered.Where(item => item.IsOutOfStock);

        if (!string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(item => string.Equals(item.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(item =>
                item.IngredientName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.StockKeepingUnit.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredIngredients.ReplaceAll(filtered.OrderBy(ing => ing.CurrentStockQuantity).ToList());
    }
}
