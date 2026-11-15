using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class OrderHistoryViewModel : BaseViewModel
{
    private readonly IOrderService _orders;
    private readonly IDialogService _dialogs;
    private List<Order> _all = new();

    public ObservableCollection<Order> Items { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;

    public OrderHistoryViewModel(IOrderService orders, IDialogService dialogs)
    {
        _orders = orders;
        _dialogs = dialogs;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        try { _all = (await _orders.GetOrdersAsync()).ToList(); ApplyFilter(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        Items.Clear();
        foreach (var o in _all.Where(o => q.Length == 0
                    || o.ReceiptNumber.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || o.CashierName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || o.TableOrCustomer.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(300))
            Items.Add(o);
    }

    [RelayCommand]
    private Task ViewAsync(Order? order)
        => order == null ? Task.CompletedTask : Shell.Current.GoToAsync("receipt", new Dictionary<string, object> { ["order"] = order });

    [RelayCommand]
    private async Task VoidAsync(Order? order)
    {
        if (order == null || order.IsVoided) return;
        var pin = await _dialogs.PromptAsync("Void order", $"Void {order.ReceiptNumber} (₱{order.TotalAmount:N2})?\nEnter an owner or manager PIN to authorize.", "PIN", numeric: true);
        if (string.IsNullOrWhiteSpace(pin)) return;
        try
        {
            await _orders.VoidOrderAsync(order.OrderId, pin);
            await _dialogs.AlertAsync("Voided", "The order was voided and the stock was restored.");
            await LoadAsync();
        }
        catch (Exception ex) when (ex is AuthException or ValidationException or UnauthorizedAccessException)
        {
            await _dialogs.AlertAsync("Cannot void", ex.Message);
        }
    }
}
