using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class ProductEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IProductService _products;
    private readonly IImageStorageService _images;
    private readonly SessionService _session;
    private readonly IDialogService _dialogs;

    private Product _product = new();
    private string _originalImage = string.Empty;
    private string? _newImagePath;

    public IReadOnlyList<string> Categories { get; } = ProductCategories.All;
    public IReadOnlyList<string> Units { get; } = new[] { "cups", "servings", "pcs", "bottles", "slices", "grams" };

    [ObservableProperty] private string _pageTitle = "New product";
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _sku = string.Empty;
    [ObservableProperty] private string _category = ProductCategories.Coffee;
    [ObservableProperty] private string _priceText = string.Empty;
    [ObservableProperty] private string _costText = string.Empty;
    [ObservableProperty] private string _stockText = "0";
    [ObservableProperty] private string _thresholdText = "10";
    [ObservableProperty] private string _unit = "cups";
    [ObservableProperty] private bool _isAvailable = true;
    [ObservableProperty] private string _imagePath = string.Empty;

    // Cost is visible to the owner always, and to managers only when registering a brand-new product.
    public bool ShowCost => _session.CanViewProfit || IsNew;
    public bool ShowInitialStock => IsNew;

    public ProductEditViewModel(IProductService products, IImageStorageService images, SessionService session, IDialogService dialogs)
    {
        _products = products;
        _images = images;
        _session = session;
        _dialogs = dialogs;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("product", out var o) && o is Product p)
        {
            _product = p.Clone();
            IsNew = false;
            PageTitle = "Edit product";
            Name = p.Name; Sku = p.Sku; Category = p.Category;
            PriceText = p.Price.ToString("0.##", CultureInfo.InvariantCulture);
            CostText = _session.CanViewProfit ? p.CostPrice.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;
            ThresholdText = p.LowStockThreshold.ToString();
            Unit = p.Unit; IsAvailable = p.IsAvailable;
            ImagePath = _originalImage = p.ImageUrl;
        }
        OnPropertyChanged(nameof(ShowCost));
        OnPropertyChanged(nameof(ShowInitialStock));
    }

    [RelayCommand] private Task TakePhotoAsync() => PickAsync(capture: true);
    [RelayCommand] private Task ChoosePhotoAsync() => PickAsync(capture: false);

    private async Task PickAsync(bool capture)
    {
        try
        {
            if (capture && !MediaPicker.Default.IsCaptureSupported)
            {
                await _dialogs.AlertAsync("Camera", "Camera is not available on this device. Choose a photo instead.");
                return;
            }
            var file = capture ? await MediaPicker.Default.CapturePhotoAsync() : await MediaPicker.Default.PickPhotoAsync();
            if (file == null) return;

            await using var stream = await file.OpenReadAsync();
            var saved = await _images.SaveProductImageAsync(stream, file.FileName);

            if (_newImagePath != null) await _images.DeleteAsync(_newImagePath);   // replaced before saving
            _newImagePath = saved;
            ImagePath = saved;
        }
        catch (PermissionException) { await _dialogs.AlertAsync("Permission needed", "Allow camera/photo access in Settings."); }
        catch (Exception ex) { await _dialogs.AlertAsync("Photo", ex.Message); }
    }

    [RelayCommand]
    private void RemovePhoto() => ImagePath = string.Empty;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = string.Empty;

        if (!TryDecimal(PriceText, out var price)) { ErrorMessage = "Enter a valid selling price."; return; }
        var cost = _product.CostPrice;
        if (ShowCost && !string.IsNullOrWhiteSpace(CostText) && !TryDecimal(CostText, out cost)) { ErrorMessage = "Enter a valid cost price."; return; }
        if (!int.TryParse(ThresholdText, out var threshold)) { ErrorMessage = "Enter a valid low-stock threshold."; return; }
        var stock = _product.StockQuantity;
        if (IsNew && !int.TryParse(StockText, out stock)) { ErrorMessage = "Enter a valid initial stock."; return; }

        try
        {
            IsBusy = true;
            var p = _product.Clone();
            p.Name = Name; p.Sku = Sku; p.Category = Category; p.Price = price; p.CostPrice = cost;
            p.StockQuantity = stock; p.LowStockThreshold = threshold; p.Unit = Unit;
            p.IsAvailable = IsAvailable; p.ImageUrl = ImagePath;

            await _products.SaveProductAsync(p);
            _newImagePath = null;   // now owned by the product
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) when (ex is ValidationException or UnauthorizedAccessException) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (_newImagePath != null) await _images.DeleteAsync(_newImagePath);   // discard unsaved photo
        await Shell.Current.GoToAsync("..");
    }

    private static bool TryDecimal(string s, out decimal value)
        => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value) || decimal.TryParse(s, out value);
}
