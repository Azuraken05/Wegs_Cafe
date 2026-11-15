using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class InventoryViewModel : BaseViewModel
{
    public const string FilterAll = "All";
    public const string FilterLow = "Low Stock";
    public const string FilterOut = "Out of Stock";

    private readonly IProductService _products;
    private readonly IInventoryService _inventory;
    private readonly SessionService _session;
    private readonly IDialogService _dialogs;
    private List<Product> _all = new();

    public ObservableCollection<ProductRow> Items { get; } = new();
    public bool CanEdit => _session.CanEditInventory;   // staff = read-only

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedFilter = FilterAll;
    [ObservableProperty] private int _lowCount;
    [ObservableProperty] private int _outCount;

    public InventoryViewModel(IProductService products, IInventoryService inventory, SessionService session, IDialogService dialogs)
    {
        _products = products;
        _inventory = inventory;
        _session = session;
        _dialogs = dialogs;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedFilterChanged(string value) => ApplyFilter();

    [RelayCommand] private void SetFilter(string filter) => SelectedFilter = filter;

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            _all = (await _products.GetProductsAsync()).ToList();
            LowCount = _all.Count(p => p.IsLowStock);
            OutCount = _all.Count(p => p.IsOutOfStock);
            ApplyFilter();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        var rows = _all
            .Where(p => SelectedFilter switch { FilterLow => p.IsLowStock, FilterOut => p.IsOutOfStock, _ => true })
            .Where(p => q.Length == 0 || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Sku.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.StockQuantity - p.LowStockThreshold)     // most urgent first
            .Select(p => new ProductRow(p, CanEdit));
        Items.Clear();
        foreach (var r in rows) Items.Add(r);
    }

    [RelayCommand]
    private async Task RestockAsync(ProductRow? row)
    {
        if (row == null || !CanEdit) return;
        var p = row.Product;
        var text = await _dialogs.PromptAsync("Restock", $"Add stock to {p.Name}\nCurrent: {p.StockText}", "Quantity to add", numeric: true);
        if (!TryPositive(text, out var qty)) return;
        await ApplyAsync(p, InventoryChangeTypes.Restock, qty);
    }

    [RelayCommand]
    private async Task AdjustAsync(ProductRow? row)
    {
        if (row == null || !CanEdit) return;
        var p = row.Product;
        var reason = await _dialogs.ActionSheetAsync($"Adjust {p.Name} (current {p.StockQuantity})", "Cancel",
            "Spoilage / Waste", "Manual Adjustment (set count)");
        if (reason == null) return;

        if (reason.StartsWith("Spoilage"))
        {
            var text = await _dialogs.PromptAsync("Spoilage / Waste", $"How many {p.Unit} were wasted?", "Quantity", numeric: true);
            if (!TryPositive(text, out var qty)) return;
            await ApplyAsync(p, InventoryChangeTypes.Spoilage, -qty);
        }
        else
        {
            var text = await _dialogs.PromptAsync("Manual adjustment", $"Enter the correct stock count for {p.Name}", "Actual count", numeric: true, initial: p.StockQuantity.ToString());
            if (!int.TryParse(text, out var count) || count < 0) { if (text != null) await _dialogs.AlertAsync("Invalid", "Enter a whole number, 0 or more."); return; }
            var delta = count - p.StockQuantity;
            if (delta == 0) return;
            await ApplyAsync(p, InventoryChangeTypes.ManualAdjustment, delta);
        }
    }

    private async Task ApplyAsync(Product p, string type, int delta)
    {
        try
        {
            await _inventory.AdjustStockAsync(p.ProductId, type, delta);
            await LoadAsync();
        }
        catch (Exception ex) when (ex is ValidationException or UnauthorizedAccessException) { await _dialogs.AlertAsync("Error", ex.Message); }
    }

    private bool TryPositive(string? text, out int qty)
    {
        qty = 0;
        if (text == null) return false;                      // cancelled
        if (int.TryParse(text, out qty) && qty > 0) return true;
        _ = _dialogs.AlertAsync("Invalid", "Enter a whole number greater than 0.");
        return false;
    }
}
