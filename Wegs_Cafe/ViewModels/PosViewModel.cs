using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class PosViewModel : BaseViewModel
{
    private readonly IProductService _products;
    private List<Product> _all = new();

    public CartService Cart { get; }
    public ObservableCollection<Product> Products { get; } = new();
    public IReadOnlyList<string> Categories { get; } = new[] { "All" }.Concat(ProductCategories.All).ToList();

    [ObservableProperty] private string _selectedCategory = "All";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsDineIn)), NotifyPropertyChangedFor(nameof(IsTakeOut))]
    private string _orderType = OrderTypes.DineIn;
    [ObservableProperty] private string _tableOrCustomer = string.Empty;
    [ObservableProperty] private bool _isCartOpen;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowCartBar))] private bool _isWide;

    public bool IsDineIn => OrderType == OrderTypes.DineIn;
    public bool IsTakeOut => OrderType == OrderTypes.TakeOut;
    public bool ShowCartBar => !IsWide && Cart.HasItems && !IsCartOpen;

    public PosViewModel(IProductService products, CartService cart)
    {
        _products = products;
        Cart = cart;
        Cart.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CartService.HasItems)) OnPropertyChanged(nameof(ShowCartBar));
        };
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();
    partial void OnIsCartOpenChanged(bool value) => OnPropertyChanged(nameof(ShowCartBar));

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            _all = (await _products.GetProductsAsync()).Where(p => p.IsAvailable).ToList();
            ApplyFilter();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        var list = _all.Where(p => SelectedCategory == "All" || p.Category == SelectedCategory)
                       .Where(p => q.Length == 0 || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Sku.Contains(q, StringComparison.OrdinalIgnoreCase));
        Products.Clear();
        foreach (var p in list) Products.Add(p);
    }

    [RelayCommand] private void SetCategory(string category) => SelectedCategory = category;
    [RelayCommand] private void SetOrderType(string type) => OrderType = type;

    [RelayCommand]
    private async Task AddToCartAsync(Product? product)
    {
        if (product == null) return;
        var error = Cart.Add(product);
        if (error != null) await Toast.Make(error).Show();
    }

    [RelayCommand]
    private async Task IncrementAsync(CartItem? line)
    {
        if (line == null) return;
        var error = Cart.Increment(line);
        if (error != null) await Toast.Make(error).Show();
    }

    [RelayCommand] private void Decrement(CartItem? line) { if (line != null) Cart.Decrement(line); if (!Cart.HasItems) IsCartOpen = false; }
    [RelayCommand] private void Remove(CartItem? line) { if (line != null) Cart.Remove(line); if (!Cart.HasItems) IsCartOpen = false; }
    [RelayCommand] private void ClearCart() { Cart.Clear(); IsCartOpen = false; }
    [RelayCommand] private void OpenCart() => IsCartOpen = true;
    [RelayCommand] private void CloseCart() => IsCartOpen = false;

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (!Cart.HasItems) return;
        IsCartOpen = false;
        var args = new Dictionary<string, object> { ["order_type"] = OrderType, ["table"] = TableOrCustomer };
        await Shell.Current.GoToAsync("payment", args);
    }

    public void ResetOrderInfo()
    {
        TableOrCustomer = string.Empty;
        OrderType = OrderTypes.DineIn;
    }
}
