using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Helpers;
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

    private readonly LocalDatabaseService _localDatabaseService;
    private readonly CloudSynchronizationService _cloudSynchronizationService;
    private List<Product> _allProductsCache = new();
    private CancellationTokenSource? _searchDebounceCts;
    private bool _hasLoadedInitialData;

    [ObservableProperty]
    private string _syncStatusText = "Synced";

    [ObservableProperty]
    private string _syncStatusIcon = MaterialIcons.Assets.CloudDone;

    [ObservableProperty]
    private Color _syncStatusColor = Color.FromArgb("#16A34A");

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
    [NotifyPropertyChangedFor(nameof(HasCustomerOrTableError))]
    private string _customerOrTableError = string.Empty;

    [ObservableProperty]
    private bool _isOrderHistoryPanelVisible;

    [ObservableProperty]
    private string _selectedPrinterStatus = "58mm Thermal Printer Ready";

    // Barista Drink Customization Modal State
    [ObservableProperty]
    private bool _isDrinkModifierModalVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomizedUnitPrice))]
    private Product? _customizingProduct;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRegularSizeSelected))]
    [NotifyPropertyChangedFor(nameof(IsLargeSizeSelected))]
    [NotifyPropertyChangedFor(nameof(CustomizedUnitPrice))]
    private string _selectedCupSize = "Regular";

    [ObservableProperty]
    private string _selectedIceLevel = "Regular Ice";

    [ObservableProperty]
    private string _selectedSweetnessLevel = "100%";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomizedUnitPrice))]
    private bool _hasExtraShotAddOn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomizedUnitPrice))]
    private bool _hasOatMilkAddOn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomizedUnitPrice))]
    private bool _hasCaramelDrizzleAddOn;

    [ObservableProperty]
    private string _customDrinkNotes = string.Empty;

    // Void Order Modal State
    [ObservableProperty]
    private bool _isVoidModalVisible;

    [ObservableProperty]
    private Order? _orderToVoid;

    [ObservableProperty]
    private string _voidSecurityPin = string.Empty;

    [ObservableProperty]
    private string _voidRemarks = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVoidPinError))]
    private string _voidPinError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVoidRemarksError))]
    private string _voidRemarksError = string.Empty;

    [ObservableProperty]
    private bool _canConfirmVoid;

    public bool HasVoidPinError => !string.IsNullOrWhiteSpace(VoidPinError);
    public bool HasVoidRemarksError => !string.IsNullOrWhiteSpace(VoidRemarksError);
    public IReadOnlyList<string> VoidReasonChips { get; } = new[]
    {
        "Wrong Order Entry",
        "Customer Cancelled",
        "Machine / Prep Issue",
        "Duplicate Checkout"
    };

    public bool IsDineInSelected => string.Equals(OrderType, OrderTypes.DineIn, StringComparison.OrdinalIgnoreCase);
    public bool IsTakeOutSelected => string.Equals(OrderType, OrderTypes.TakeOut, StringComparison.OrdinalIgnoreCase);
    public bool HasCustomerOrTableError => !string.IsNullOrWhiteSpace(CustomerOrTableError);
    public bool IsRegularSizeSelected => string.Equals(SelectedCupSize, "Regular", StringComparison.OrdinalIgnoreCase);
    public bool IsLargeSizeSelected => string.Equals(SelectedCupSize, "Large", StringComparison.OrdinalIgnoreCase);
    public bool HasActiveQueueOrders => ActiveQueueOrders.Count > 0;
    public bool CanProceedToCheckout => _orderCartService.HasOrderItems && !HasCustomerOrTableError && (CustomerOrTableLabel?.Trim().Length ?? 0) >= 2;

    public decimal CurrentModifierSurcharge
    {
        get
        {
            var surcharge = 0m;
            if (IsLargeSizeSelected) surcharge += 25m;
            if (HasExtraShotAddOn) surcharge += 30m;
            if (HasOatMilkAddOn) surcharge += 35m;
            if (HasCaramelDrizzleAddOn) surcharge += 20m;
            return surcharge;
        }
    }

    public decimal CustomizedUnitPrice => (CustomizingProduct?.SellingPrice ?? 0m) + CurrentModifierSurcharge;

    public OrderCartService Cart => _orderCartService;
    public IReadOnlyList<string> Categories { get; } = ProductCategories.AllWithFilter;
    public IReadOnlyList<string> IceLevelOptions { get; } = new[] { "Regular Ice", "Less Ice", "No Ice" };
    public IReadOnlyList<string> SweetnessOptions { get; } = new[] { "100%", "75%", "50%", "0%" };

    public ObservableRangeCollection<Product> FilteredProducts { get; } = new();
    public ObservableRangeCollection<Order> ActiveQueueOrders { get; } = new();
    public ObservableRangeCollection<Order> RecentCompletedAndVoidedOrders { get; } = new();

    public string ActiveBaristaDisplay => _sessionService.ActiveUser != null
        ? $"Barista: {_sessionService.ActiveUser.FullName} ({_sessionService.ActiveUser.RoleName})"
        : "Barista: Active";

    public event EventHandler? DrinkModifierModalRequested;

    public PointOfSaleViewModel(
        IProductManagementService productManagementService,
        IOrderTransactionService orderTransactionService,
        IInventoryManagementService inventoryManagementService,
        IReceiptPrinterService receiptPrinterService,
        OrderCartService orderCartService,
        SessionService sessionService,
        IDialogService dialogService,
        LocalDatabaseService localDatabaseService,
        CloudSynchronizationService cloudSynchronizationService)
    {
        _productManagementService = productManagementService;
        _orderTransactionService = orderTransactionService;
        _inventoryManagementService = inventoryManagementService;
        _receiptPrinterService = receiptPrinterService;
        _orderCartService = orderCartService;
        _sessionService = sessionService;
        _dialogService = dialogService;
        _localDatabaseService = localDatabaseService;
        _cloudSynchronizationService = cloudSynchronizationService;
        PageTitle = "Point of Sale";

        _inventoryManagementService.InventoryChanged += async (_, _) => await RefreshPointOfSaleDataAsync();
        _sessionService.ActiveUserChanged += (_, _) => OnPropertyChanged(nameof(ActiveBaristaDisplay));
        _orderCartService.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanProceedToCheckout));
        _cloudSynchronizationService.SyncStateChanged += OnCloudSyncStateChanged;

        SyncStatusText = _cloudSynchronizationService.StatusText;
        SyncStatusIcon = _cloudSynchronizationService.StatusIcon;
        SyncStatusColor = _cloudSynchronizationService.StatusColor;
    }

    private void OnCloudSyncStateChanged(CloudSyncStatus status, int pendingCount, string statusText, string statusIcon, Color statusColor)
    {
        SyncStatusText = statusText;
        SyncStatusIcon = statusIcon;
        SyncStatusColor = statusColor;
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
                    MainThread.BeginInvokeOnMainThread(ApplyProductFilters);
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    partial void OnSelectedCategoryChanged(string value) => ApplyProductFilters();

    partial void OnCustomerOrTableLabelChanged(string value)
    {
        CustomerOrTableError = (value?.Trim().Length ?? 0) < 2
            ? "Customer Name or Table Number (min 2 chars) is required."
            : string.Empty;
        OnPropertyChanged(nameof(CanProceedToCheckout));
    }

    [RelayCommand]
    public async Task LoadPointOfSaleDataAsync()
    {
        if (_hasLoadedInitialData || IsBusy) return;
        _hasLoadedInitialData = true;
        await RefreshPointOfSaleDataAsync();
    }

    public async Task RefreshPointOfSaleDataAsync()
    {
        if (IsBusy) return;
        ClearError();

        try
        {
            IsBusy = true;
            var (products, recentOrders, activeQueue, printerSummary) = await Task.Run(async () =>
            {
                var loadedProducts = (await _productManagementService.GetProductsAsync().ConfigureAwait(false)).ToList();
                var loadedIngredients = (await _inventoryManagementService.GetIngredientsAsync().ConfigureAwait(false)).ToList();

                foreach (var product in loadedProducts)
                {
                    if (product.HasRecipe)
                    {
                        var minServings = int.MaxValue;
                        foreach (var recipeItem in product.RecipeIngredients)
                        {
                            var raw = loadedIngredients.FirstOrDefault(i => i.IngredientIdentifier == recipeItem.IngredientIdentifier);
                            if (raw != null && recipeItem.QuantityRequired > 0m)
                            {
                                var possible = (int)Math.Floor(raw.CurrentStockQuantity / recipeItem.QuantityRequired);
                                minServings = Math.Min(minServings, possible);
                            }
                            else
                            {
                                minServings = 0;
                            }
                        }
                        product.CanMakeServings = minServings == int.MaxValue ? 0 : minServings;
                    }
                    else
                    {
                        product.CanMakeServings = product.StockQuantity;
                    }
                }

                var loadedOrders = (await _orderTransactionService.GetOrdersAsync().ConfigureAwait(false)).Take(25).ToList();
                var queue = loadedOrders.Where(order =>
                    !order.IsVoided &&
                    (order.PreparationStatus == OrderPreparationStatuses.Preparing ||
                     order.PreparationStatus == OrderPreparationStatuses.Ready)).ToList();

                var printers = await _receiptPrinterService.GetPairedPrintersAsync().ConfigureAwait(false);
                var activePrinter = _receiptPrinterService.SelectedPrinter ?? printers.FirstOrDefault();
                var summary = activePrinter != null
                    ? $"{activePrinter.DeviceName} ({_receiptPrinterService.PaperWidthMillimeters}mm)"
                    : "Thermal Printer Ready";

                return (loadedProducts, loadedOrders, queue, summary);
            }).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                OnPropertyChanged(nameof(ActiveBaristaDisplay));
                _allProductsCache = products;
                ApplyProductFilters();
                RecentCompletedAndVoidedOrders.ReplaceAll(recentOrders);
                ActiveQueueOrders.ReplaceAll(activeQueue);
                OnPropertyChanged(nameof(HasActiveQueueOrders));
                SelectedPrinterStatus = printerSummary;
            });

            _ = _cloudSynchronizationService.SynchronizeAsync();
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => ErrorMessage = ex.Message);
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
    }

    [RelayCommand]
    private async Task ShowSyncStatusAsync()
    {
        var pending = _cloudSynchronizationService.PendingCount;
        var online = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        var statusMessage = online
            ? $"Connected to Firebase Realtime Database.\nSync status: {_cloudSynchronizationService.CurrentStatus}\nPending queued operations: {pending}"
            : $"Device is offline. {pending} operation(s) queued safely in local SQLite.";

        var retry = await _dialogService.ShowConfirmationAsync("Cloud Sync Status", statusMessage, "Sync Now", "Close");
        if (retry)
        {
            await _cloudSynchronizationService.SynchronizeAsync();
            await RefreshPointOfSaleDataAsync();
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
    private async Task HandleProductTileTappedAsync(Product? product)
    {
        if (product is null) return;

        if (!product.IsAvailable)
        {
            await _dialogService.ShowAlertAsync("Sold Out", $"{product.ProductName} is currently marked Sold Out. Tap 'Available' on the card to re-enable.");
            return;
        }

        if (product.StockQuantity <= 0)
        {
            await _dialogService.ShowAlertAsync("Out of Stock", $"{product.ProductName} has 0 stock remaining.");
            return;
        }

        if (product.AllowDrinkModifiers)
        {
            CustomizingProduct = product;
            SelectedCupSize = "Regular";
            SelectedIceLevel = product.Category == ProductCategories.HotCoffee ? "No Ice" : "Regular Ice";
            SelectedSweetnessLevel = "100%";
            HasExtraShotAddOn = false;
            HasOatMilkAddOn = false;
            HasCaramelDrizzleAddOn = false;
            CustomDrinkNotes = string.Empty;
            IsDrinkModifierModalVisible = true;
            DrinkModifierModalRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        var warning = _orderCartService.AddProduct(product);
        if (!string.IsNullOrWhiteSpace(warning))
        {
            await _dialogService.ShowAlertAsync("Stock Limit Reached", warning);
        }
    }

    [RelayCommand]
    private void SelectCupSize(string size)
    {
        SelectedCupSize = string.Equals(size, "Large", StringComparison.OrdinalIgnoreCase) ? "Large" : "Regular";
    }

    [RelayCommand]
    private void CloseDrinkModifierModal()
    {
        IsDrinkModifierModalVisible = false;
        CustomizingProduct = null;
    }

    [RelayCommand]
    private async Task ConfirmCustomizedDrinkAsync()
    {
        if (CustomizingProduct is null) return;

        var addOns = new List<string>();
        if (HasExtraShotAddOn) addOns.Add("Extra Shot");
        if (HasOatMilkAddOn) addOns.Add("Oat Milk");
        if (HasCaramelDrizzleAddOn) addOns.Add("Caramel Drizzle");

        var warning = _orderCartService.AddProduct(
            CustomizingProduct,
            SelectedCupSize,
            SelectedIceLevel,
            SelectedSweetnessLevel,
            addOns,
            CurrentModifierSurcharge,
            CustomDrinkNotes);

        IsDrinkModifierModalVisible = false;
        CustomizingProduct = null;

        if (!string.IsNullOrWhiteSpace(warning))
        {
            await _dialogService.ShowAlertAsync("Stock Limit Reached", warning);
        }
    }

    [RelayCommand]
    private async Task ToggleProductAvailabilityAsync(Product? product)
    {
        if (product is null) return;

        try
        {
            var targetStatus = !product.IsAvailable;
            await _productManagementService.ToggleProductAvailabilityAsync(product.ProductIdentifier, targetStatus);
            product.IsAvailable = targetStatus;

            var cachedItem = _allProductsCache.FirstOrDefault(p => p.ProductIdentifier == product.ProductIdentifier);
            if (cachedItem != null)
            {
                cachedItem.IsAvailable = targetStatus;
            }

            ApplyProductFilters();
            _ = _cloudSynchronizationService.SynchronizeAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Availability Update Failed", ex.Message);
        }
    }

    [RelayCommand]
    private Task ToggleProductSoldOutAsync(Product? product) => ToggleProductAvailabilityAsync(product);

    [RelayCommand]
    private async Task AdvanceOrderPreparationStatusAsync(Order? order)
    {
        if (order is null) return;

        var nextStatus = order.PreparationStatus == OrderPreparationStatuses.Preparing
            ? OrderPreparationStatuses.Ready
            : OrderPreparationStatuses.Completed;

        try
        {
            await _orderTransactionService.UpdateOrderPreparationStatusAsync(order.OrderIdentifier, nextStatus);
            await LoadPointOfSaleDataAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Queue Update Failed", ex.Message);
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
            $"Enter preparation notes for {item.Product.ProductName}:",
            "Less sweet, extra hot",
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
        CustomerOrTableError = string.Empty;
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

        if ((CustomerOrTableLabel?.Trim().Length ?? 0) < 2)
        {
            CustomerOrTableError = "Customer Name or Table Number (min 2 chars) is required.";
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            ["OrderType"] = OrderType,
            ["CustomerOrTableLabel"] = CustomerOrTableLabel?.Trim() ?? string.Empty
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

    partial void OnVoidSecurityPinChanged(string value) => ValidateVoidModalLive();
    partial void OnVoidRemarksChanged(string value) => ValidateVoidModalLive();

    private void ValidateVoidModalLive()
    {
        var validation = ValidationRules.ValidateVoidOrder(VoidSecurityPin, VoidRemarks);
        VoidPinError = validation.GetError("SecurityPin");
        VoidRemarksError = validation.GetError("VoidRemarks");
        CanConfirmVoid = validation.IsValid;
    }

    [RelayCommand]
    private void SelectVoidReason(string reason)
    {
        VoidRemarks = reason;
        ValidateVoidModalLive();
    }

    [RelayCommand]
    private void OpenVoidModal(Order? order)
    {
        if (order is null) return;
        if (order.IsVoided)
        {
            _dialogService.ShowAlertAsync("Already Voided", $"Order {order.ReceiptNumber} has already been voided.");
            return;
        }

        OrderToVoid = order;
        VoidSecurityPin = string.Empty;
        VoidRemarks = string.Empty;
        VoidPinError = string.Empty;
        VoidRemarksError = string.Empty;
        CanConfirmVoid = false;
        IsVoidModalVisible = true;
    }

    [RelayCommand]
    private void CloseVoidModal()
    {
        IsVoidModalVisible = false;
        OrderToVoid = null;
    }

    [RelayCommand]
    private async Task ConfirmVoidOrderAsync()
    {
        if (OrderToVoid is null || IsBusy) return;
        ValidateVoidModalLive();
        if (!CanConfirmVoid) return;

        try
        {
            IsBusy = true;
            var voided = await _orderTransactionService.VoidOrderAsync(OrderToVoid.OrderIdentifier, VoidSecurityPin.Trim(), VoidRemarks.Trim());
            IsVoidModalVisible = false;
            OrderToVoid = null;
            await RefreshPointOfSaleDataAsync();
            await _dialogService.ShowAlertAsync(
                "Order Voided",
                $"Order {voided.ReceiptNumber} has been marked VOIDED and all recipe ingredient quantities were restored to inventory.");
        }
        catch (Exception ex)
        {
            VoidPinError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void VoidOrderTransaction(Order? order)
    {
        OpenVoidModal(order);
    }

    private void ApplyProductFilters()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filtered = _allProductsCache.AsEnumerable();

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

        FilteredProducts.ReplaceAll(filtered.ToList());
    }
}
