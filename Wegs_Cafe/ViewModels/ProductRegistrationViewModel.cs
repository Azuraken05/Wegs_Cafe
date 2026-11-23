using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class ProductRegistrationViewModel : BaseViewModel
{
    private readonly IProductManagementService _productManagementService;
    private readonly IInventoryManagementService _inventoryManagementService;
    private readonly IImageStorageService _imageStorageService;
    private readonly IDialogService _dialogService;

    private List<Product> _allProducts = new();
    private List<Ingredient> _allRawIngredients = new();
    private CancellationTokenSource? _searchDebounceCts;
    private CancellationTokenSource? _validationDebounceCts;
    private bool _hasLoadedInitialData;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedFilterCategory = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormSectionTitle))]
    [NotifyPropertyChangedFor(nameof(SaveButtonLabel))]
    private string _editingProductIdentifier = string.Empty;

    [ObservableProperty]
    private string _stockKeepingUnit = string.Empty;

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = ProductCategories.HotCoffee;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CalculatedProfitMargin))]
    [NotifyPropertyChangedFor(nameof(GrossProfitMarginPercent))]
    [NotifyPropertyChangedFor(nameof(ProfitBadgeText))]
    [NotifyPropertyChangedFor(nameof(ProfitBadgeColor))]
    private string _sellingPriceText = string.Empty;

    [ObservableProperty]
    private string _costPriceText = "0.00";

    [ObservableProperty]
    private string _stockQuantityText = "25";

    [ObservableProperty]
    private string _lowStockThresholdText = "10";

    [ObservableProperty]
    private string _measurementUnit = "cups";

    [ObservableProperty]
    private bool _allowDrinkModifiers = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedImage))]
    private string _imageUrl = string.Empty;

    [ObservableProperty]
    private bool _isAvailable = true;

    // Recipe Ingredient Builder State
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedIngredientUnitLabel))]
    private Ingredient? _selectedRecipeIngredient;

    [ObservableProperty]
    private string _recipeQuantityRequiredText = "200";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CalculatedProfitMargin))]
    [NotifyPropertyChangedFor(nameof(GrossProfitMarginPercent))]
    [NotifyPropertyChangedFor(nameof(ProfitBadgeText))]
    [NotifyPropertyChangedFor(nameof(ProfitBadgeColor))]
    private decimal _calculatedRecipeCost;

    [ObservableProperty]
    private string _recipeBuilderError = string.Empty;

    // Inline Validation Errors
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProductNameError))]
    private string _productNameError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStockKeepingUnitError))]
    private string _stockKeepingUnitError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSellingPriceError))]
    private string _sellingPriceError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCostPriceError))]
    private string _costPriceError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStockQuantityError))]
    private string _stockQuantityError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLowStockThresholdError))]
    private string _lowStockThresholdError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitForm))]
    private bool _isFormValid;

    public bool HasSelectedImage => !string.IsNullOrWhiteSpace(ImageUrl);
    public bool HasProductNameError => !string.IsNullOrWhiteSpace(ProductNameError);
    public bool HasStockKeepingUnitError => !string.IsNullOrWhiteSpace(StockKeepingUnitError);
    public bool HasSellingPriceError => !string.IsNullOrWhiteSpace(SellingPriceError);
    public bool HasCostPriceError => !string.IsNullOrWhiteSpace(CostPriceError);
    public bool HasStockQuantityError => !string.IsNullOrWhiteSpace(StockQuantityError);
    public bool HasLowStockThresholdError => !string.IsNullOrWhiteSpace(LowStockThresholdError);
    public bool HasRecipeBuilderError => !string.IsNullOrWhiteSpace(RecipeBuilderError);
    public bool CanSubmitForm => IsFormValid && IsNotBusy;

    public string SelectedIngredientUnitLabel => SelectedRecipeIngredient?.MeasurementUnit ?? "units";

    public decimal ParsedSellingPrice
    {
        get
        {
            return decimal.TryParse(SellingPriceText?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price) && price > 0m
                ? price
                : 0m;
        }
    }

    public decimal CalculatedProfitMargin => ParsedSellingPrice > 0m ? ParsedSellingPrice - CalculatedRecipeCost : 0m;

    public decimal GrossProfitMarginPercent => ParsedSellingPrice > 0m
        ? (CalculatedProfitMargin / ParsedSellingPrice) * 100m
        : 0m;

    public string ProfitBadgeText
    {
        get
        {
            if (ParsedSellingPrice <= 0m && CalculatedRecipeCost <= 0m)
                return "Attach ingredients to view recipe food cost & profit margin.";
            return $"Recipe Cost: ₱{CalculatedRecipeCost:N2} | Gross Profit: ₱{CalculatedProfitMargin:N2} ({GrossProfitMarginPercent:0.#}%)";
        }
    }

    public Color ProfitBadgeColor
    {
        get
        {
            if (ParsedSellingPrice <= 0m) return Color.FromArgb("#52525B");
            if (GrossProfitMarginPercent >= 60m) return Color.FromArgb("#16A34A"); // Green
            if (GrossProfitMarginPercent >= 30m) return Color.FromArgb("#D97706"); // Amber
            return Color.FromArgb("#DC2626"); // Red
        }
    }

    public string FormSectionTitle => string.IsNullOrWhiteSpace(EditingProductIdentifier)
        ? "Register New Menu Product"
        : "Update Menu Product";

    public string SaveButtonLabel => string.IsNullOrWhiteSpace(EditingProductIdentifier)
        ? "Register Product"
        : "Save Product Changes";

    public IReadOnlyList<string> AvailableCategories { get; } = ProductCategories.All;
    public IReadOnlyList<string> FilterCategories { get; } = ProductCategories.AllWithFilter;

    public ObservableRangeCollection<Product> FilteredProducts { get; } = new();
    public ObservableRangeCollection<Ingredient> AvailableRawIngredients { get; } = new();
    public ObservableCollection<RecipeIngredientItem> CurrentRecipeIngredients { get; } = new();

    public ProductRegistrationViewModel(
        IProductManagementService productManagementService,
        IInventoryManagementService inventoryManagementService,
        IImageStorageService imageStorageService,
        IDialogService dialogService)
    {
        _productManagementService = productManagementService;
        _inventoryManagementService = inventoryManagementService;
        _imageStorageService = imageStorageService;
        _dialogService = dialogService;
        PageTitle = "Product Registration";

        ValidateFormLive();
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

    partial void OnSelectedFilterCategoryChanged(string value) => ApplyFilters();
    partial void OnProductNameChanged(string value) => TriggerDebouncedValidation();
    partial void OnStockKeepingUnitChanged(string value) => TriggerDebouncedValidation();
    partial void OnSelectedCategoryChanged(string value)
    {
        AllowDrinkModifiers = value is ProductCategories.HotCoffee or ProductCategories.IcedCoffee or ProductCategories.NonCoffee;
        TriggerDebouncedValidation();
    }
    partial void OnSellingPriceTextChanged(string value)
    {
        TriggerDebouncedValidation();
        OnPropertyChanged(nameof(ParsedSellingPrice));
        OnPropertyChanged(nameof(CalculatedProfitMargin));
        OnPropertyChanged(nameof(GrossProfitMarginPercent));
        OnPropertyChanged(nameof(ProfitBadgeText));
        OnPropertyChanged(nameof(ProfitBadgeColor));
    }
    partial void OnCostPriceTextChanged(string value) => TriggerDebouncedValidation();
    partial void OnStockQuantityTextChanged(string value) => TriggerDebouncedValidation();
    partial void OnLowStockThresholdTextChanged(string value) => TriggerDebouncedValidation();

    private void TriggerDebouncedValidation()
    {
        _validationDebounceCts?.Cancel();
        _validationDebounceCts = new CancellationTokenSource();
        var token = _validationDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150, token);
                if (!token.IsCancellationRequested)
                {
                    MainThread.BeginInvokeOnMainThread(ValidateFormLive);
                }
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private void ValidateFormLive()
    {
        var existingSkus = _allProducts
            .Where(p => p.ProductIdentifier != EditingProductIdentifier)
            .Select(p => p.StockKeepingUnit);

        var summary = ValidationRules.ValidateProductRegistration(
            ProductName,
            StockKeepingUnit,
            SelectedCategory,
            SellingPriceText,
            CostPriceText,
            StockQuantityText,
            LowStockThresholdText,
            existingSkus);

        ProductNameError = summary.GetError("ProductName");
        StockKeepingUnitError = summary.GetError("StockKeepingUnit");
        SellingPriceError = summary.GetError("SellingPrice");
        CostPriceError = summary.GetError("CostPrice");
        StockQuantityError = summary.GetError("StockQuantity");
        LowStockThresholdError = summary.GetError("LowStockThreshold");
        IsFormValid = summary.IsValid;
    }

    [RelayCommand]
    public async Task LoadCatalogAsync()
    {
        if (_hasLoadedInitialData || IsBusy) return;
        _hasLoadedInitialData = true;
        await RefreshCatalogAsync();
    }

    public async Task RefreshCatalogAsync()
    {
        if (IsBusy) return;
        ClearError();

        try
        {
            IsBusy = true;
            var products = await _productManagementService.GetProductsAsync();
            _allProducts = products.ToList();
            ApplyFilters();

            var rawIngredients = await _inventoryManagementService.GetIngredientsAsync();
            _allRawIngredients = rawIngredients.ToList();
            AvailableRawIngredients.ReplaceAll(_allRawIngredients);

            if (SelectedRecipeIngredient == null && AvailableRawIngredients.Count > 0)
                SelectedRecipeIngredient = AvailableRawIngredients[0];

            if (string.IsNullOrWhiteSpace(StockKeepingUnit))
                GenerateNextSku();

            RecalculateRecipeFoodCost();
            ValidateFormLive();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSubmitForm));
        }
    }

    [RelayCommand]
    private void AddRecipeIngredient()
    {
        if (SelectedRecipeIngredient == null)
        {
            RecipeBuilderError = "Please select a raw ingredient.";
            return;
        }

        if (!decimal.TryParse(RecipeQuantityRequiredText?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0m)
        {
            RecipeBuilderError = "Enter a valid quantity greater than 0.";
            return;
        }

        RecipeBuilderError = string.Empty;

        var existingItem = CurrentRecipeIngredients.FirstOrDefault(i => i.IngredientIdentifier == SelectedRecipeIngredient.IngredientIdentifier);
        if (existingItem != null)
        {
            existingItem.QuantityRequired = quantity;
            existingItem.MeasurementUnit = SelectedRecipeIngredient.MeasurementUnit;
            // Force collection notification
            var index = CurrentRecipeIngredients.IndexOf(existingItem);
            CurrentRecipeIngredients[index] = existingItem.Clone();
        }
        else
        {
            CurrentRecipeIngredients.Add(new RecipeIngredientItem
            {
                IngredientIdentifier = SelectedRecipeIngredient.IngredientIdentifier,
                IngredientName = SelectedRecipeIngredient.IngredientName,
                MeasurementUnit = SelectedRecipeIngredient.MeasurementUnit,
                QuantityRequired = quantity
            });
        }

        RecalculateRecipeFoodCost();
    }

    [RelayCommand]
    private void RemoveRecipeIngredient(RecipeIngredientItem? item)
    {
        if (item == null) return;
        CurrentRecipeIngredients.Remove(item);
        RecalculateRecipeFoodCost();
    }

    private void RecalculateRecipeFoodCost()
    {
        decimal totalCost = 0m;
        foreach (var recipeItem in CurrentRecipeIngredients)
        {
            var raw = _allRawIngredients.FirstOrDefault(r => r.IngredientIdentifier == recipeItem.IngredientIdentifier);
            var unitCost = raw?.UnitCost ?? 0m;
            totalCost += unitCost * recipeItem.QuantityRequired;
        }

        CalculatedRecipeCost = totalCost;
        if (totalCost > 0m)
        {
            CostPriceText = totalCost.ToString("0.00", CultureInfo.InvariantCulture);
        }

        OnPropertyChanged(nameof(CalculatedProfitMargin));
        OnPropertyChanged(nameof(GrossProfitMarginPercent));
        OnPropertyChanged(nameof(ProfitBadgeText));
        OnPropertyChanged(nameof(ProfitBadgeColor));
    }

    [RelayCommand]
    private void SelectFilterCategory(string category)
    {
        SelectedFilterCategory = string.IsNullOrWhiteSpace(category) ? "All" : category;
    }

    [RelayCommand]
    private void EditProduct(Product? product)
    {
        if (product is null) return;
        ClearError();

        EditingProductIdentifier = product.ProductIdentifier;
        StockKeepingUnit = product.StockKeepingUnit;
        ProductName = product.ProductName;
        SelectedCategory = product.Category;
        SellingPriceText = product.SellingPrice.ToString("0.00");
        CostPriceText = product.CostPrice.ToString("0.00");
        StockQuantityText = product.StockQuantity.ToString();
        LowStockThresholdText = product.LowStockThreshold.ToString();
        MeasurementUnit = product.MeasurementUnit;
        AllowDrinkModifiers = product.AllowDrinkModifiers;
        ImageUrl = product.ImageUrl;
        IsAvailable = product.IsAvailable;

        CurrentRecipeIngredients.Clear();
        if (product.RecipeIngredients != null)
        {
            foreach (var ing in product.RecipeIngredients)
            {
                CurrentRecipeIngredients.Add(ing.Clone());
            }
        }

        RecalculateRecipeFoodCost();
        ValidateFormLive();
    }

    [RelayCommand]
    private void ResetProductForm()
    {
        ClearError();
        EditingProductIdentifier = string.Empty;
        ProductName = string.Empty;
        SelectedCategory = ProductCategories.HotCoffee;
        SellingPriceText = string.Empty;
        CostPriceText = "0.00";
        StockQuantityText = "25";
        LowStockThresholdText = "10";
        MeasurementUnit = "cups";
        AllowDrinkModifiers = true;
        ImageUrl = string.Empty;
        IsAvailable = true;
        CurrentRecipeIngredients.Clear();
        CalculatedRecipeCost = 0m;
        RecipeBuilderError = string.Empty;
        GenerateNextSku();
        ValidateFormLive();
    }

    [RelayCommand]
    private async Task PickImageFromGalleryAsync()
    {
        try
        {
            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Select Product Photo (.jpg, .png, .webp under 5 MB)",
                SelectionLimit = 1
            });

            var photo = photos?.FirstOrDefault();
            if (photo is null) return;

            await using var stream = await photo.OpenReadAsync();
            ImageUrl = await _imageStorageService.UploadProductImageAsync(stream, photo.FileName);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Image Upload Error", ex.Message);
        }
    }

    [RelayCommand]
    private async Task CaptureImageWithCameraAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await PickImageFromGalleryAsync();
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Capture Product Photo"
            });

            if (photo is null) return;

            await using var stream = await photo.OpenReadAsync();
            ImageUrl = await _imageStorageService.UploadProductImageAsync(stream, photo.FileName);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Camera Error", ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveProductAsync()
    {
        if (IsBusy) return;
        ClearError();
        ValidateFormLive();

        if (!IsFormValid)
        {
            ErrorMessage = "Please fix the highlighted validation errors before saving.";
            return;
        }

        try
        {
            IsBusy = true;
            OnPropertyChanged(nameof(CanSubmitForm));

            var effectiveCost = CalculatedRecipeCost > 0m
                ? CalculatedRecipeCost
                : (string.IsNullOrWhiteSpace(CostPriceText) ? 0m : decimal.Parse(CostPriceText.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture));

            var product = new Product
            {
                ProductIdentifier = EditingProductIdentifier,
                StockKeepingUnit = StockKeepingUnit.Trim().ToUpperInvariant(),
                ProductName = ProductName.Trim(),
                Category = SelectedCategory,
                SellingPrice = decimal.Parse(SellingPriceText.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture),
                CostPrice = effectiveCost,
                CalculatedRecipeCost = CalculatedRecipeCost,
                StockQuantity = int.Parse(StockQuantityText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture),
                LowStockThreshold = int.Parse(LowStockThresholdText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture),
                MeasurementUnit = string.IsNullOrWhiteSpace(MeasurementUnit) ? "cups" : MeasurementUnit.Trim(),
                AllowDrinkModifiers = AllowDrinkModifiers,
                ImageUrl = ImageUrl,
                IsAvailable = IsAvailable,
                RecipeIngredients = CurrentRecipeIngredients.ToList()
            };

            var saved = await _productManagementService.SaveProductAsync(product);
            var products = await _productManagementService.GetProductsAsync();
            _allProducts = products.ToList();
            ApplyFilters();

            var wasNew = string.IsNullOrWhiteSpace(EditingProductIdentifier);
            ResetProductForm();

            await _dialogService.ShowAlertAsync(
                wasNew ? "Product Registered" : "Product Updated",
                $"{saved.ProductName} ({saved.StockKeepingUnit}) with recipe has been saved.");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSubmitForm));
        }
    }

    [RelayCommand]
    private async Task DeleteProductAsync(Product? product)
    {
        if (product is null) return;

        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Delete Product",
            $"Remove {product.ProductName} ({product.StockKeepingUnit}) from the catalog?",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        try
        {
            IsBusy = true;
            await _productManagementService.DeleteProductAsync(product.ProductIdentifier);
            var products = await _productManagementService.GetProductsAsync();
            _allProducts = products.ToList();
            ApplyFilters();

            if (EditingProductIdentifier == product.ProductIdentifier)
                ResetProductForm();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Delete Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilters()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filtered = _allProducts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedFilterCategory) &&
            !string.Equals(SelectedFilterCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(item => string.Equals(item.Category, SelectedFilterCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(item =>
                item.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.StockKeepingUnit.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredProducts.ReplaceAll(filtered.ToList());
    }

    private void GenerateNextSku()
    {
        var nextIndex = _allProducts.Count + 1;
        StockKeepingUnit = $"WEGS-PRD-{nextIndex:D2}";
    }
}
