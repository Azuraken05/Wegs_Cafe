using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class ProductRegistrationViewModel : BaseViewModel
{
    private readonly IProductManagementService _productManagementService;
    private readonly IImageStorageService _imageStorageService;
    private readonly IDialogService _dialogService;

    private List<Product> _allProducts = new();

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
    private string _selectedCategory = ProductCategories.Coffee;

    [ObservableProperty]
    private string _sellingPriceText = string.Empty;

    [ObservableProperty]
    private string _costPriceText = string.Empty;

    [ObservableProperty]
    private string _stockQuantityText = "25";

    [ObservableProperty]
    private string _lowStockThresholdText = "10";

    [ObservableProperty]
    private string _measurementUnit = "cups";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedImage))]
    private string _imageUrl = string.Empty;

    [ObservableProperty]
    private bool _isAvailable = true;

    public bool HasSelectedImage => !string.IsNullOrWhiteSpace(ImageUrl);
    public string FormSectionTitle => string.IsNullOrWhiteSpace(EditingProductIdentifier)
        ? "Register New Menu Product"
        : "Update Menu Product";
    public string SaveButtonLabel => string.IsNullOrWhiteSpace(EditingProductIdentifier)
        ? "Register Product"
        : "Save Product Changes";

    public IReadOnlyList<string> AvailableCategories { get; } = ProductCategories.All;
    public IReadOnlyList<string> FilterCategories { get; } = ProductCategories.AllWithFilter;
    public IReadOnlyList<string> CommonMeasurementUnits { get; } = new[] { "cups", "servings", "pcs", "plates", "bottles" };

    public ObservableCollection<Product> FilteredProducts { get; } = new();

    public ProductRegistrationViewModel(
        IProductManagementService productManagementService,
        IImageStorageService imageStorageService,
        IDialogService dialogService)
    {
        _productManagementService = productManagementService;
        _imageStorageService = imageStorageService;
        _dialogService = dialogService;
        PageTitle = "Product Registration & Menu Catalog";
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedFilterCategoryChanged(string value) => ApplyFilters();

    [RelayCommand]
    public async Task LoadCatalogAsync()
    {
        if (IsBusy) return;
        ClearError();

        try
        {
            IsBusy = true;
            var products = await _productManagementService.GetProductsAsync();
            _allProducts = products.ToList();
            ApplyFilters();

            if (string.IsNullOrWhiteSpace(StockKeepingUnit))
            {
                GenerateNextSku();
            }
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
        ImageUrl = product.ImageUrl;
        IsAvailable = product.IsAvailable;
    }

    [RelayCommand]
    private void ResetProductForm()
    {
        ClearError();
        EditingProductIdentifier = string.Empty;
        ProductName = string.Empty;
        SelectedCategory = ProductCategories.Coffee;
        SellingPriceText = string.Empty;
        CostPriceText = string.Empty;
        StockQuantityText = "25";
        LowStockThresholdText = "10";
        MeasurementUnit = "cups";
        ImageUrl = string.Empty;
        IsAvailable = true;
        GenerateNextSku();
    }

    [RelayCommand]
    private async Task PickImageFromGalleryAsync()
    {
        try
        {
            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Select Product Photo",
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

        if (!decimal.TryParse(SellingPriceText, out var sellingPrice) || sellingPrice <= 0)
        {
            ErrorMessage = "Please enter a valid selling price greater than zero.";
            return;
        }

        if (!decimal.TryParse(CostPriceText, out var costPrice) || costPrice < 0)
        {
            ErrorMessage = "Please enter a valid unit cost price (0 or greater).";
            return;
        }

        if (!int.TryParse(StockQuantityText, out var stockQty) || stockQty < 0)
        {
            ErrorMessage = "Please enter a valid stock quantity.";
            return;
        }

        if (!int.TryParse(LowStockThresholdText, out var lowThreshold) || lowThreshold < 0)
        {
            ErrorMessage = "Please enter a valid low-stock threshold.";
            return;
        }

        try
        {
            IsBusy = true;
            var product = new Product
            {
                ProductIdentifier = EditingProductIdentifier,
                StockKeepingUnit = StockKeepingUnit.Trim(),
                ProductName = ProductName.Trim(),
                Category = SelectedCategory,
                SellingPrice = sellingPrice,
                CostPrice = costPrice,
                StockQuantity = stockQty,
                LowStockThreshold = lowThreshold,
                MeasurementUnit = string.IsNullOrWhiteSpace(MeasurementUnit) ? "pcs" : MeasurementUnit.Trim(),
                ImageUrl = ImageUrl,
                IsAvailable = IsAvailable
            };

            var saved = await _productManagementService.SaveProductAsync(product);
            var products = await _productManagementService.GetProductsAsync();
            _allProducts = products.ToList();
            ApplyFilters();

            var wasNew = string.IsNullOrWhiteSpace(EditingProductIdentifier);
            ResetProductForm();

            await _dialogService.ShowAlertAsync(
                wasNew ? "Product Registered" : "Product Updated",
                $"{saved.ProductName} ({saved.StockKeepingUnit}) has been saved to the catalog.");
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

        FilteredProducts.Clear();
        foreach (var item in filtered)
            FilteredProducts.Add(item);
    }

    private void GenerateNextSku()
    {
        var nextIndex = _allProducts.Count + 1;
        StockKeepingUnit = $"WEGS-{nextIndex:D3}";
    }
}
