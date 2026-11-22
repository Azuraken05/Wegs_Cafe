using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;
using Wegs_Cafe.Views;

namespace Wegs_Cafe.ViewModels;

public partial class PointOfSaleViewModel : BaseViewModel
{
    private readonly IProductManagementService _productManagementService;
    private readonly IOrderTransactionService _orderTransactionService;
    private readonly IInventoryManagementService _inventoryManagementService;
    private readonly IReceiptPrinterService _receiptPrinterService;
    private readonly OrderCartService _orderCartService;
    private readonly SessionService _sessionService;
    private readonly IDialogService _dialogService;

    private List<Product> _allProducts = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDineInSelected))]
    [NotifyPropertyChangedFor(nameof(IsTakeOutSelected))]
    private string _orderType = OrderTypes.DineIn;

    [ObservableProperty]
    private string _customerOrTableLabel = string.Empty;

    [ObservableProperty]
    private bool _isMobileCartDrawerExpanded;

    [ObservableProperty]
    private bool _isOrderHistoryPanelVisible;

    [ObservableProperty]
    private string _selectedPrinterStatus = "58mm Thermal Printer Ready";

    public bool IsDineInSelected => string.Equals(OrderType, OrderTypes.DineIn, StringComparison.OrdinalIgnoreCase);
    public bool IsTakeOutSelected => string.Equals(OrderType, OrderTypes.TakeOut, StringComparison.OrdinalIgnoreCase);

    public OrderCartService Cart => _orderCartService;
    public IReadOnlyList<string> Categories { get; } = ProductCategories.AllWithFilter;
    public ObservableCollection<Product> FilteredProducts { get; } = new();
    public ObservableCollection<Order> RecentCompletedAndVoidedOrders { get; } = new();

    public string ActiveCashierDisplay => _sessionService.ActiveUser != null
        ? $"Cashier: {_sessionService.ActiveUser.FullName} ({_sessionService.ActiveUser.Role.ToUpperInvariant()})"
        : "Cashier: Active";

    public PointOfSaleViewModel(
        IProductManagementService productManagementService,
        IOrderTransactionService orderTransactionService,
        IInventoryManagementService inventoryManagementService,
        IReceiptPrinterService receiptPrinterService,
        OrderCartService orderCartService,
        SessionService sessionService,
        IDialogService dialogService)
    {
        _productManagementService = productManagementService;
        _orderTransactionService = orderTransactionService;
        _inventoryManagementService = inventoryManagementService;
        _receiptPrinterService = receiptPrinterService;
        _orderCartService = orderCartService;
        _sessionService = sessionService;
        _dialogService = dialogService;
        PageTitle = "Point of Sale Terminal";

        _inventoryManagementService.InventoryChanged += async (_, _) => await LoadPointOfSaleDataAsync();
        _sessionService.ActiveUserChanged += (_, _) => OnPropertyChanged(nameof(ActiveCashierDisplay));
    }

    partial void OnSearchQueryChanged(string value) => ApplyProductFilters();
    partial void OnSelectedCategoryChanged(string value) => ApplyProductFilters();

    [RelayCommand]
    public async Task LoadPointOfSaleDataAsync()
    {
        if (IsBusy) return;
        ClearError();

        try
        {
            IsBusy = true;
            OnPropertyChanged(nameof(ActiveCashierDisplay));
            var products = await _productManagementService.GetProductsAsync();
            _allProducts = products.Where(product => product.IsAvailable).ToList();
            ApplyProductFilters();

            var orders = await _orderTransactionService.GetOrdersAsync();
            RecentCompletedAndVoidedOrders.Clear();
            foreach (var order in orders.Take(25))
                RecentCompletedAndVoidedOrders.Add(order);

            var printers = await _receiptPrinterService.GetPairedPrintersAsync();
            var activePrinter = _receiptPrinterService.SelectedPrinter ?? printers.FirstOrDefault();
            SelectedPrinterStatus = activePrinter != null
                ? $"{activePrinter.DeviceName} ({_receiptPrinterService.PaperWidthMillimeters}mm)"
                : "Thermal Printer Ready";
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
    private void SelectCategory(string category)
    {
        SelectedCategory = string.IsNullOrWhiteSpace(category) ? "All" : category;
    }

    [RelayCommand]
    private void SelectOrderType(string type)
    {
        OrderType = string.Equals(type, OrderTypes.TakeOut, StringComparison.OrdinalIgnoreCase)
            ? OrderTypes.TakeOut
            : OrderTypes.DineIn;
    }

    [RelayCommand]
    private async Task AddProductToCartAsync(Product? product)
    {
        if (product is null) return;

        var warning = _orderCartService.AddProduct(product);
        if (!string.IsNullOrWhiteSpace(warning))
        {
            await _dialogService.ShowAlertAsync("Stock Limit Reached", warning);
        }
    }

    [RelayCommand]
    private async Task IncrementCartItemAsync(OrderCartItem? item)
    {
        if (item is null) return;

        var warning = _orderCartService.IncrementItem(item);
        if (!string.IsNullOrWhiteSpace(warning))
        {
            await _dialogService.ShowAlertAsync("Stock Limit Reached", warning);
        }
    }

    [RelayCommand]
    private void DecrementCartItem(OrderCartItem? item)
    {
        if (item is null) return;
        _orderCartService.DecrementItem(item);
    }

    [RelayCommand]
    private void RemoveCartItem(OrderCartItem? item)
    {
        if (item is null) return;
        _orderCartService.RemoveItem(item);
    }

    [RelayCommand]
    private async Task EditItemNotesAsync(OrderCartItem? item)
    {
        if (item is null) return;

        var notes = await _dialogService.ShowPromptAsync(
            "Preparation Notes",
            $"Enter preparation notes for {item.Product.ProductName} (e.g., Less sweet, Oat milk, Extra hot):",
            "Less sweet, extra shot",
            numeric: false,
            initialValue: item.PreparationNotes);

        if (notes is null) return;
        item.PreparationNotes = notes.Trim();
    }

    [RelayCommand]
    private async Task ClearCurrentCartAsync()
    {
        if (!_orderCartService.HasOrderItems) return;

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Clear Order Cart",
            "Remove all items from the current order cart?",
            "Clear Cart",
            "Cancel");

        if (!confirm) return;
        _orderCartService.ClearCart();
        CustomerOrTableLabel = string.Empty;
    }

    [RelayCommand]
    private void ToggleMobileCartDrawer()
    {
        IsMobileCartDrawerExpanded = !IsMobileCartDrawerExpanded;
    }

    [RelayCommand]
    private void ToggleOrderHistoryPanel()
    {
        IsOrderHistoryPanelVisible = !IsOrderHistoryPanelVisible;
    }

    [RelayCommand]
    private async Task ConfigureBluetoothPrinterAsync()
    {
        var printers = await _receiptPrinterService.GetPairedPrintersAsync();
        var options = printers.Select(printer => $"{printer.DeviceName} [{printer.DeviceAddress}]")
            .Concat(new[] { "Set Paper Width: 58mm (32 chars)", "Set Paper Width: 80mm (48 chars)", "Print Diagnostic Test Receipt" })
            .ToArray();

        var selection = await _dialogService.ShowActionSheetAsync("Thermal Receipt Printer Configuration", "Close", options);
        if (string.IsNullOrWhiteSpace(selection) || selection == "Close") return;

        if (selection.StartsWith("Set Paper Width: 58mm", StringComparison.OrdinalIgnoreCase))
        {
            _receiptPrinterService.PaperWidthMillimeters = 58;
        }
        else if (selection.StartsWith("Set Paper Width: 80mm", StringComparison.OrdinalIgnoreCase))
        {
            _receiptPrinterService.PaperWidthMillimeters = 80;
        }
        else if (selection.Equals("Print Diagnostic Test Receipt", StringComparison.OrdinalIgnoreCase))
        {
            await _receiptPrinterService.PrintTestReceiptAsync();
            await _dialogService.ShowAlertAsync("Test Receipt Sent", "Sent ESC/POS test receipt to the selected thermal printer.");
        }
        else
        {
            var matched = printers.FirstOrDefault(printer => selection.Contains(printer.DeviceAddress, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                _receiptPrinterService.SelectPrinter(matched);
            }
        }

        var active = _receiptPrinterService.SelectedPrinter;
        SelectedPrinterStatus = active != null
            ? $"{active.DeviceName} ({_receiptPrinterService.PaperWidthMillimeters}mm)"
            : "Thermal Printer Ready";
    }

    [RelayCommand]
    private async Task ProceedToCheckoutAsync()
    {
        if (!_orderCartService.HasOrderItems)
        {
            await _dialogService.ShowAlertAsync("Empty Order Cart", "Please add at least one product to the order cart before checking out.");
            return;
        }

        IsMobileCartDrawerExpanded = false;
        var parameters = new Dictionary<string, object>
        {
            ["OrderType"] = OrderType,
            ["CustomerOrTableLabel"] = CustomerOrTableLabel ?? string.Empty
        };

        await Shell.Current.GoToAsync(nameof(PaymentCheckoutPage), parameters);
    }

    [RelayCommand]
    private async Task ViewOrderReceiptAsync(Order? order)
    {
        if (order is null) return;

        var parameters = new Dictionary<string, object>
        {
            ["CompletedOrder"] = order
        };

        await Shell.Current.GoToAsync(nameof(ReceiptPreviewPage), parameters);
    }

    [RelayCommand]
    private async Task VoidOrderTransactionAsync(Order? order)
    {
        if (order is null) return;

        if (order.IsVoided)
        {
            await _dialogService.ShowAlertAsync("Already Voided", $"Order {order.ReceiptNumber} has already been voided.");
            return;
        }

        var pin = await _dialogService.ShowPromptAsync(
            "Manager or Owner Security PIN Required",
            $"Enter Manager or Owner 4-6 digit Security PIN to void {order.ReceiptNumber} and restore stock:",
            "1234",
            numeric: true);

        if (string.IsNullOrWhiteSpace(pin)) return;

        try
        {
            IsBusy = true;
            var voided = await _orderTransactionService.VoidOrderAsync(order.OrderIdentifier, pin.Trim());
            await LoadPointOfSaleDataAsync();
            await _dialogService.ShowAlertAsync(
                "Order Voided",
                $"Order {voided.ReceiptNumber} has been marked VOIDED and all item quantities were restored to inventory.");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Void Authorization Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyProductFilters()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filtered = _allProducts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedCategory) &&
            !string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(product => string.Equals(product.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(product =>
                product.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                product.StockKeepingUnit.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                product.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredProducts.Clear();
        foreach (var item in filtered)
            FilteredProducts.Add(item);
    }
}
