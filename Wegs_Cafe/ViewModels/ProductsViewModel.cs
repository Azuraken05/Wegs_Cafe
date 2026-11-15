using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class ProductsViewModel : BaseViewModel
{
    private readonly IProductService _products;
    private readonly SessionService _session;
    private readonly IDialogService _dialogs;
    private List<Product> _all = new();

    public ObservableCollection<ProductRow> Items { get; } = new();
    public bool CanManage => _session.CanManageProducts;

    [ObservableProperty] private string _searchText = string.Empty;

    public ProductsViewModel(IProductService products, SessionService session, IDialogService dialogs)
    {
        _products = products;
        _session = session;
        _dialogs = dialogs;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            _all = (await _products.GetProductsAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        var rows = _all.Where(p => q.Length == 0
                || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || p.Sku.Contains(q, StringComparison.OrdinalIgnoreCase)
                || p.Category.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Select(p => new ProductRow(p, CanManage));
        Items.Clear();
        foreach (var r in rows) Items.Add(r);
    }

    [RelayCommand]
    private Task AddAsync() => OpenEditorAsync(null);

    [RelayCommand]
    private Task EditAsync(ProductRow? row) => row == null ? Task.CompletedTask : OpenEditorAsync(row.Product);

    private Task OpenEditorAsync(Product? product)
    {
        if (!CanManage) return Task.CompletedTask;
        var args = new Dictionary<string, object>();
        if (product != null) args["product"] = product;
        return Shell.Current.GoToAsync("productedit", args);
    }

    [RelayCommand]
    private async Task DeleteAsync(ProductRow? row)
    {
        if (row == null || !CanManage) return;
        if (!await _dialogs.ConfirmAsync("Delete product", $"Delete \"{row.Product.Name}\"? This cannot be undone.", "Delete")) return;
        try
        {
            await _products.DeleteProductAsync(row.Product.ProductId);
            await LoadAsync();
        }
        catch (Exception ex) when (ex is ValidationException or UnauthorizedAccessException) { await _dialogs.AlertAsync("Error", ex.Message); }
    }
}
