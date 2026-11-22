using System.Globalization;
using Firebase.Database.Query;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Atomic data store for products, inventory_logs, and orders using the full snake_case schema,
/// with Firebase Realtime Database synchronization for Laravel interoperability.
/// </summary>
public class CatalogAndOrderStore : IProductManagementService, IInventoryManagementService, IOrderTransactionService
{
    private readonly SessionService _sessionService;
    private readonly IAuditTrailService _auditTrailService;
    private readonly IImageStorageService _imageStorageService;
    private readonly IAuthenticationService _authenticationService;
    private readonly FirebaseService _firebaseService;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    private readonly string _productsFilePath = Path.Combine(FileSystem.AppDataDirectory, "products_v3.json");
    private readonly string _inventoryLogsFilePath = Path.Combine(FileSystem.AppDataDirectory, "inventory_logs_v3.json");
    private readonly string _ordersFilePath = Path.Combine(FileSystem.AppDataDirectory, "orders_v3.json");

    private List<Product>? _cachedProducts;
    private List<Order>? _cachedOrders;
    private List<InventoryLog>? _cachedInventoryLogs;

    public event EventHandler? InventoryChanged;

    public CatalogAndOrderStore(
        SessionService sessionService,
        IAuditTrailService auditTrailService,
        IImageStorageService imageStorageService,
        IAuthenticationService authenticationService,
        FirebaseService firebaseService)
    {
        _sessionService = sessionService;
        _auditTrailService = auditTrailService;
        _imageStorageService = imageStorageService;
        _authenticationService = authenticationService;
        _firebaseService = firebaseService;
    }

    private void RaiseInventoryChanged()
        => MainThread.BeginInvokeOnMainThread(() => InventoryChanged?.Invoke(this, EventArgs.Empty));

    // ================= PRODUCTS =================

    public async Task<IReadOnlyList<Product>> GetProductsAsync()
    {
        RequireAuthenticated();
        await _mutex.WaitAsync();
        try
        {
            var list = await EnsureProductsSeededAsync();
            return list
                .OrderBy(product => product.Category)
                .ThenBy(product => product.ProductName)
                .Select(FilterSensitiveFields)
                .ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<Product> SaveProductAsync(Product inputProduct)
    {
        if (!_sessionService.CanRegisterProducts)
            throw new UnauthorizedAccessException("You do not have permission to register or edit products.");

        Product savedProduct;
        string? auditAction = null;
        string? auditDetails = null;
        string? priceAuditDetails = null;

        await _mutex.WaitAsync();
        try
        {
            var products = await EnsureProductsSeededAsync();
            var existing = products.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(inputProduct.ProductIdentifier) &&
                p.ProductIdentifier == inputProduct.ProductIdentifier);

            var existingSkus = products
                .Where(p => p.ProductIdentifier != existing?.ProductIdentifier)
                .Select(p => p.StockKeepingUnit);

            var validation = ValidationRules.ValidateProductRegistration(
                inputProduct.ProductName,
                inputProduct.StockKeepingUnit,
                inputProduct.Category,
                inputProduct.SellingPrice.ToString(CultureInfo.InvariantCulture),
                inputProduct.CostPrice.ToString(CultureInfo.InvariantCulture),
                inputProduct.StockQuantity.ToString(CultureInfo.InvariantCulture),
                inputProduct.LowStockThreshold.ToString(CultureInfo.InvariantCulture),
                existingSkus);

            if (!validation.IsValid)
                throw new ValidationException(validation.FirstErrorOrEmpty);

            savedProduct = inputProduct.Clone();
            savedProduct.StockKeepingUnit = inputProduct.StockKeepingUnit.Trim().ToUpperInvariant();
            savedProduct.ProductName = inputProduct.ProductName.Trim();
            savedProduct.UpdatedAt = IsoTimestamp.Now();

            if (existing == null)
            {
                savedProduct.ProductIdentifier = Guid.NewGuid().ToString("N");
                products.Add(savedProduct);
                await JsonStorageFile.WriteListAsync(_productsFilePath, products);

                if (savedProduct.StockQuantity > 0)
                {
                    await AppendInventoryLogInternalAsync(
                        savedProduct,
                        InventoryMovementTypes.StockReplenishment,
                        savedProduct.StockQuantity,
                        0,
                        savedProduct.StockQuantity,
                        "Initial product registration stock");
                }

                auditAction = AuditActionTypes.CreateProduct;
                auditDetails = $"Registered product '{savedProduct.ProductName}' ({savedProduct.StockKeepingUnit}) at ₱{savedProduct.SellingPrice:N2}, initial stock {savedProduct.StockQuantity} {savedProduct.MeasurementUnit}.";
            }
            else
            {
                savedProduct.StockQuantity = existing.StockQuantity;
                if (!_sessionService.CanViewCostAndProfitMargins && savedProduct.CostPrice <= 0)
                    savedProduct.CostPrice = existing.CostPrice;

                var index = products.IndexOf(existing);
                products[index] = savedProduct;
                await JsonStorageFile.WriteListAsync(_productsFilePath, products);

                if (existing.SellingPrice != savedProduct.SellingPrice)
                {
                    priceAuditDetails = $"Updated selling price for '{savedProduct.ProductName}' from ₱{existing.SellingPrice:N2} to ₱{savedProduct.SellingPrice:N2}.";
                }

                if (existing.ImageUrl != savedProduct.ImageUrl && !string.IsNullOrWhiteSpace(existing.ImageUrl))
                    await _imageStorageService.DeleteProductImageAsync(existing.ImageUrl);

                auditAction = AuditActionTypes.UpdateProduct;
                auditDetails = $"Updated product '{savedProduct.ProductName}' ({savedProduct.StockKeepingUnit}).";
            }
        }
        finally
        {
            _mutex.Release();
        }

        if (priceAuditDetails != null)
        {
            await _auditTrailService.RecordAuditEntryAsync(
                AuditActionTypes.UpdatePrice,
                $"products/{savedProduct.ProductIdentifier}",
                priceAuditDetails);
        }

        if (auditAction != null && auditDetails != null)
        {
            await _auditTrailService.RecordAuditEntryAsync(
                auditAction,
                $"products/{savedProduct.ProductIdentifier}",
                auditDetails);
        }

        RaiseInventoryChanged();
        await SyncToFirebaseAsync(DatabaseNodes.Products, savedProduct.ProductIdentifier, savedProduct);
        return FilterSensitiveFields(savedProduct);
    }

    public async Task<Product> ToggleProductAvailabilityAsync(string productIdentifier, bool isAvailable)
    {
        if (!_sessionService.CanAccessPointOfSale)
            throw new UnauthorizedAccessException("Please sign in to update item availability.");

        Product updatedProduct;
        await _mutex.WaitAsync();
        try
        {
            var products = await EnsureProductsSeededAsync();
            var product = products.FirstOrDefault(p => p.ProductIdentifier == productIdentifier)
                          ?? throw new ValidationException("Product was not found.");

            product.IsAvailable = isAvailable;
            product.UpdatedAt = IsoTimestamp.Now();
            updatedProduct = product.Clone();
            await JsonStorageFile.WriteListAsync(_productsFilePath, products);
        }
        finally
        {
            _mutex.Release();
        }

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.ToggleProductAvailability,
            $"products/{productIdentifier}",
            $"Marked '{updatedProduct.ProductName}' as {(isAvailable ? "Available" : "Sold Out / Unavailable")}.");

        RaiseInventoryChanged();
        await SyncToFirebaseAsync(DatabaseNodes.Products, updatedProduct.ProductIdentifier, updatedProduct);
        return FilterSensitiveFields(updatedProduct);
    }

    public async Task DeleteProductAsync(string productIdentifier)
    {
        if (!_sessionService.CanRegisterProducts)
            throw new UnauthorizedAccessException("You do not have permission to delete products.");

        Product deletedProduct;
        await _mutex.WaitAsync();
        try
        {
            var products = await EnsureProductsSeededAsync();
            deletedProduct = products.FirstOrDefault(p => p.ProductIdentifier == productIdentifier)
                             ?? throw new ValidationException("Product was not found.");

            products.Remove(deletedProduct);
            await JsonStorageFile.WriteListAsync(_productsFilePath, products);

            if (!string.IsNullOrWhiteSpace(deletedProduct.ImageUrl))
                await _imageStorageService.DeleteProductImageAsync(deletedProduct.ImageUrl);
        }
        finally
        {
            _mutex.Release();
        }

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.DeleteProduct,
            $"products/{productIdentifier}",
            $"Deleted product '{deletedProduct.ProductName}' ({deletedProduct.StockKeepingUnit}).");

        RaiseInventoryChanged();
        await DeleteFromFirebaseAsync(DatabaseNodes.Products, productIdentifier);
    }

    // ================= INVENTORY =================

    public async Task<Product> RecordStockMovementAsync(
        string productIdentifier,
        string movementType,
        int quantityChanged,
        string remarks = "")
    {
        if (!_sessionService.CanRestockInventory)
            throw new UnauthorizedAccessException("You do not have permission to restock or adjust inventory.");

        Product updatedProduct;
        int previousQuantity;
        int newQuantity;

        await _mutex.WaitAsync();
        try
        {
            var products = await EnsureProductsSeededAsync();
            var product = products.FirstOrDefault(p => p.ProductIdentifier == productIdentifier)
                          ?? throw new ValidationException("Product was not found.");

            var validation = ValidationRules.ValidateInventoryAdjustment(
                product.StockQuantity,
                movementType,
                quantityChanged.ToString(CultureInfo.InvariantCulture),
                remarks);

            if (!validation.IsValid)
                throw new ValidationException(validation.FirstErrorOrEmpty);

            previousQuantity = product.StockQuantity;
            newQuantity = previousQuantity + quantityChanged;

            product.StockQuantity = newQuantity;
            product.UpdatedAt = IsoTimestamp.Now();
            updatedProduct = product.Clone();

            await JsonStorageFile.WriteListAsync(_productsFilePath, products);
            await AppendInventoryLogInternalAsync(
                product,
                movementType,
                quantityChanged,
                previousQuantity,
                newQuantity,
                remarks?.Trim() ?? string.Empty);
        }
        finally
        {
            _mutex.Release();
        }

        var auditAction = movementType == InventoryMovementTypes.StockReplenishment
            ? AuditActionTypes.StockReplenishment
            : AuditActionTypes.ManualStockAdjustment;

        await _auditTrailService.RecordAuditEntryAsync(
            auditAction,
            $"products/{productIdentifier}",
            $"{movementType}: '{updatedProduct.ProductName}' stock changed from {previousQuantity} to {newQuantity} ({quantityChanged:+#;-#;0} {updatedProduct.MeasurementUnit}).");

        RaiseInventoryChanged();
        await SyncToFirebaseAsync(DatabaseNodes.Products, updatedProduct.ProductIdentifier, updatedProduct);
        return FilterSensitiveFields(updatedProduct);
    }

    public async Task<IReadOnlyList<InventoryLog>> GetInventoryLogsAsync()
    {
        if (!_sessionService.CanViewInventory)
            throw new UnauthorizedAccessException("You do not have permission to view inventory logs.");

        await _mutex.WaitAsync().ConfigureAwait(false);
        try
        {
            var logs = _cachedInventoryLogs ??= await JsonStorageFile.ReadListAsync<InventoryLog>(_inventoryLogsFilePath).ConfigureAwait(false);
            return logs.OrderByDescending(log => log.RecordedAt).ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    // ================= ORDERS & ATOMIC CHECKOUT =================

    public async Task<Order> ProcessCheckoutAsync(CheckoutTransactionRequest request)
    {
        if (!_sessionService.CanAccessPointOfSale)
            throw new UnauthorizedAccessException("Please sign in to process Point of Sale transactions.");
        if (request.OrderItems.Count == 0)
            throw new ValidationException("The order cart is empty.");
        if (request.OrderType != OrderTypes.DineIn && request.OrderType != OrderTypes.TakeOut)
            throw new ValidationException("Please select Dine-In or Take-Out.");

        Order completedOrder;
        int totalItemsCount;

        await _mutex.WaitAsync();
        try
        {
            var products = await EnsureProductsSeededAsync();
            var orders = await EnsureOrdersSeededAsync();

            var orderItems = new List<OrderItem>();
            var stockDeductions = new List<(Product product, int quantity)>();

            foreach (var cartLine in request.OrderItems)
            {
                var product = products.FirstOrDefault(p => p.ProductIdentifier == cartLine.Product.ProductIdentifier)
                              ?? throw new ValidationException($"Product '{cartLine.Product.ProductName}' no longer exists.");

                if (cartLine.Quantity <= 0)
                    throw new ValidationException("Item quantity must be at least 1.");
                if (!product.IsAvailable)
                    throw new ValidationException($"'{product.ProductName}' is currently marked Sold Out / Unavailable.");
                if (product.StockQuantity < cartLine.Quantity)
                    throw new ValidationException($"Insufficient stock for '{product.ProductName}'. Remaining: {product.StockQuantity} {product.MeasurementUnit}.");

                var unitPriceWithModifiers = product.SellingPrice + cartLine.ModifierSurcharge;
                orderItems.Add(new OrderItem
                {
                    ProductIdentifier = product.ProductIdentifier,
                    ProductName = product.ProductName,
                    Quantity = cartLine.Quantity,
                    UnitPrice = unitPriceWithModifiers,
                    LineSubtotal = unitPriceWithModifiers * cartLine.Quantity,
                    CupSize = cartLine.CupSize,
                    IceLevel = cartLine.IceLevel,
                    SweetnessLevel = cartLine.SweetnessLevel,
                    AddOns = cartLine.AddOns.ToList(),
                    ModifierSurcharge = cartLine.ModifierSurcharge,
                    PreparationNotes = cartLine.PreparationNotes?.Trim() ?? string.Empty
                });

                stockDeductions.Add((product, cartLine.Quantity));
            }

            var subtotalAmount = orderItems.Sum(line => line.LineSubtotal);
            var discountAmount = request.DiscountAmount;
            if (discountAmount < 0 || discountAmount > subtotalAmount)
                throw new ValidationException("Discount amount must be between ₱0.00 and the subtotal.");
            if (discountAmount > 0 && !_sessionService.CanRegisterProducts)
                throw new UnauthorizedAccessException("Only the Owner or permitted roles can apply discounts.");

            var totalAmount = subtotalAmount - discountAmount;

            var checkoutValidation = ValidationRules.ValidatePointOfSaleCheckout(
                orderItems.Count,
                request.CustomerOrTableLabel,
                request.PaymentMethod,
                totalAmount,
                request.AmountTendered.ToString(CultureInfo.InvariantCulture),
                request.PaymentReferenceNumber);

            if (!checkoutValidation.IsValid)
                throw new ValidationException(checkoutValidation.FirstErrorOrEmpty);

            var amountTendered = request.PaymentMethod == PaymentMethods.Cash ? request.AmountTendered : totalAmount;
            var changeDue = request.PaymentMethod == PaymentMethods.Cash ? amountTendered - totalAmount : 0m;
            var referenceNumber = request.PaymentMethod == PaymentMethods.Cash
                ? string.Empty
                : request.PaymentReferenceNumber?.Trim() ?? string.Empty;

            var datePrefix = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var sequenceNumber = orders.Count(o => o.ReceiptNumber.StartsWith($"WEGS-{datePrefix}-")) + 1;

            completedOrder = new Order
            {
                OrderIdentifier = Guid.NewGuid().ToString("N"),
                ReceiptNumber = $"WEGS-{datePrefix}-{sequenceNumber:0000}",
                OrderType = request.OrderType,
                CustomerOrTableLabel = request.CustomerOrTableLabel?.Trim() ?? string.Empty,
                PreparationStatus = OrderPreparationStatuses.Preparing,
                OrderItems = orderItems,
                SubtotalAmount = subtotalAmount,
                DiscountAmount = discountAmount,
                TotalAmount = totalAmount,
                PaymentMethod = request.PaymentMethod,
                PaymentReferenceNumber = referenceNumber,
                AmountTendered = amountTendered,
                ChangeDue = changeDue,
                OrderStatus = OrderStatuses.Completed,
                BaristaIdentifier = _sessionService.CurrentUser!.UserIdentifier,
                BaristaName = _sessionService.CurrentUser!.FullName,
                CreatedAt = IsoTimestamp.Now()
            };

            orders.Add(completedOrder);

            var inventoryLogs = _cachedInventoryLogs ??= await JsonStorageFile.ReadListAsync<InventoryLog>(_inventoryLogsFilePath).ConfigureAwait(false);
            foreach (var (product, quantitySold) in stockDeductions)
            {
                var previousStock = product.StockQuantity;
                product.StockQuantity = previousStock - quantitySold;
                product.UpdatedAt = IsoTimestamp.Now();

                inventoryLogs.Add(new InventoryLog
                {
                    InventoryLogIdentifier = Guid.NewGuid().ToString("N"),
                    ProductIdentifier = product.ProductIdentifier,
                    ProductName = product.ProductName,
                    MovementType = InventoryMovementTypes.SaleDeduction,
                    QuantityChanged = -quantitySold,
                    PreviousStockQuantity = previousStock,
                    NewStockQuantity = product.StockQuantity,
                    Remarks = $"Order {completedOrder.ReceiptNumber}",
                    PerformedByIdentifier = _sessionService.CurrentUser!.UserIdentifier,
                    PerformedByName = _sessionService.CurrentUser!.FullName,
                    RecordedAt = IsoTimestamp.Now()
                });
            }

            await JsonStorageFile.WriteListAsync(_ordersFilePath, orders);
            await JsonStorageFile.WriteListAsync(_productsFilePath, products);
            await JsonStorageFile.WriteListAsync(_inventoryLogsFilePath, inventoryLogs);

            totalItemsCount = orderItems.Sum(i => i.Quantity);
        }
        finally
        {
            _mutex.Release();
        }

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.PointOfSaleCheckout,
            $"orders/{completedOrder.OrderIdentifier}",
            $"Completed checkout {completedOrder.ReceiptNumber}: ₱{completedOrder.TotalAmount:N2} via {completedOrder.PaymentMethod} ({totalItemsCount} items).");

        RaiseInventoryChanged();
        await SyncToFirebaseAsync(DatabaseNodes.Orders, completedOrder.OrderIdentifier, completedOrder);
        return completedOrder;
    }

    public async Task<IReadOnlyList<Order>> GetOrdersAsync()
    {
        RequireAuthenticated();
        await _mutex.WaitAsync();
        try
        {
            var allOrders = await EnsureOrdersSeededAsync();
            return allOrders.OrderByDescending(o => o.CreatedAt).ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<Order> UpdateOrderPreparationStatusAsync(string orderIdentifier, string preparationStatus)
    {
        if (!_sessionService.CanAccessPointOfSale)
            throw new UnauthorizedAccessException("Please sign in to update order preparation status.");

        Order updatedOrder;
        await _mutex.WaitAsync();
        try
        {
            var orders = await EnsureOrdersSeededAsync();
            var targetOrder = orders.FirstOrDefault(o => o.OrderIdentifier == orderIdentifier)
                              ?? throw new ValidationException("Order was not found.");

            targetOrder.PreparationStatus = preparationStatus;
            updatedOrder = targetOrder;
            await JsonStorageFile.WriteListAsync(_ordersFilePath, orders);
        }
        finally
        {
            _mutex.Release();
        }

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.UpdateOrderPreparationStatus,
            $"orders/{orderIdentifier}",
            $"Updated order {updatedOrder.ReceiptNumber} preparation status to '{preparationStatus}'.");

        await SyncToFirebaseAsync(DatabaseNodes.Orders, updatedOrder.OrderIdentifier, updatedOrder);
        return updatedOrder;
    }

    public async Task<Order> VoidOrderAsync(string orderIdentifier, string securityPin)
    {
        RequireAuthenticated();
        var authorizer = await _authenticationService.AuthorizeSecurityPinAsync(securityPin);

        Order voidedOrder;
        await _mutex.WaitAsync();
        try
        {
            var orders = await EnsureOrdersSeededAsync();
            var targetOrder = orders.FirstOrDefault(o => o.OrderIdentifier == orderIdentifier)
                              ?? throw new ValidationException("Order was not found.");

            if (targetOrder.OrderStatus == OrderStatuses.Voided)
                throw new ValidationException("This order has already been voided.");

            var products = await EnsureProductsSeededAsync();
            var inventoryLogs = _cachedInventoryLogs ??= await JsonStorageFile.ReadListAsync<InventoryLog>(_inventoryLogsFilePath).ConfigureAwait(false);

            foreach (var item in targetOrder.OrderItems)
            {
                var product = products.FirstOrDefault(p => p.ProductIdentifier == item.ProductIdentifier);
                if (product == null) continue;

                var previousStock = product.StockQuantity;
                product.StockQuantity = previousStock + item.Quantity;
                product.UpdatedAt = IsoTimestamp.Now();

                inventoryLogs.Add(new InventoryLog
                {
                    InventoryLogIdentifier = Guid.NewGuid().ToString("N"),
                    ProductIdentifier = product.ProductIdentifier,
                    ProductName = product.ProductName,
                    MovementType = InventoryMovementTypes.ManualCorrection,
                    QuantityChanged = item.Quantity,
                    PreviousStockQuantity = previousStock,
                    NewStockQuantity = product.StockQuantity,
                    Remarks = $"Voided Order {targetOrder.ReceiptNumber}",
                    PerformedByIdentifier = authorizer.UserIdentifier,
                    PerformedByName = authorizer.FullName,
                    RecordedAt = IsoTimestamp.Now()
                });
            }

            targetOrder.OrderStatus = OrderStatuses.Voided;
            targetOrder.PreparationStatus = OrderStatuses.Voided;
            targetOrder.VoidedAt = IsoTimestamp.Now();
            targetOrder.VoidedByIdentifier = authorizer.UserIdentifier;
            voidedOrder = targetOrder;

            await JsonStorageFile.WriteListAsync(_ordersFilePath, orders);
            await JsonStorageFile.WriteListAsync(_productsFilePath, products);
            await JsonStorageFile.WriteListAsync(_inventoryLogsFilePath, inventoryLogs);
        }
        finally
        {
            _mutex.Release();
        }

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.VoidOrder,
            $"orders/{voidedOrder.OrderIdentifier}",
            $"Voided order {voidedOrder.ReceiptNumber} (₱{voidedOrder.TotalAmount:N2}) and restored stock. Authorized by {authorizer.FullName} ({authorizer.RoleDisplayName}).");

        RaiseInventoryChanged();
        await SyncToFirebaseAsync(DatabaseNodes.Orders, voidedOrder.OrderIdentifier, voidedOrder);
        return voidedOrder;
    }

    // ================= SEEDING & HELPERS =================

    private async Task AppendInventoryLogInternalAsync(
        Product product,
        string movementType,
        int quantityChanged,
        int previousQuantity,
        int newQuantity,
        string remarks)
    {
        var logs = _cachedInventoryLogs ??= await JsonStorageFile.ReadListAsync<InventoryLog>(_inventoryLogsFilePath).ConfigureAwait(false);
        var log = new InventoryLog
        {
            InventoryLogIdentifier = Guid.NewGuid().ToString("N"),
            ProductIdentifier = product.ProductIdentifier,
            ProductName = product.ProductName,
            MovementType = movementType,
            QuantityChanged = quantityChanged,
            PreviousStockQuantity = previousQuantity,
            NewStockQuantity = newQuantity,
            Remarks = remarks,
            PerformedByIdentifier = _sessionService.CurrentUser?.UserIdentifier ?? "system",
            PerformedByName = _sessionService.CurrentUser?.FullName ?? "System",
            RecordedAt = IsoTimestamp.Now()
        };
        logs.Add(log);
        await JsonStorageFile.WriteListAsync(_inventoryLogsFilePath, logs);
        await SyncToFirebaseAsync(DatabaseNodes.InventoryLogs, log.InventoryLogIdentifier, log);
    }

    private async Task SyncToFirebaseAsync<T>(string node, string key, T payload)
    {
        try
        {
            await _firebaseService.RealtimeDatabaseClient
                .Child(node)
                .Child(key)
                .PutAsync(payload);
        }
        catch
        {
            // Offline-first resilience: local JSON remains authoritative when offline
        }
    }

    private async Task DeleteFromFirebaseAsync(string node, string key)
    {
        try
        {
            await _firebaseService.RealtimeDatabaseClient
                .Child(node)
                .Child(key)
                .DeleteAsync();
        }
        catch
        {
            // Offline-first resilience
        }
    }

    private Product FilterSensitiveFields(Product product)
    {
        var clone = product.Clone();
        if (!_sessionService.CanViewCostAndProfitMargins)
            clone.CostPrice = 0m;
        return clone;
    }

    private void RequireAuthenticated()
    {
        if (!_sessionService.IsAuthenticated)
            throw new UnauthorizedAccessException("Please sign in to continue.");
    }

    private async Task<List<Product>> EnsureProductsSeededAsync()
    {
        if (_cachedProducts != null) return _cachedProducts;

        if (File.Exists(_productsFilePath))
        {
            var existing = await JsonStorageFile.ReadListAsync<Product>(_productsFilePath).ConfigureAwait(false);
            if (existing.Count > 0)
            {
                _cachedProducts = existing;
                return _cachedProducts;
            }
        }

        var seededProducts = new List<Product>
        {
            new() { ProductIdentifier = "product-spanish-latte-hot", StockKeepingUnit = "WEGS-HOT-01", ProductName = "Hot Spanish Latte", Category = ProductCategories.HotCoffee, SellingPrice = 140m, CostPrice = 45m, StockQuantity = 42, LowStockThreshold = 10, MeasurementUnit = "cups", AllowDrinkModifiers = true },
            new() { ProductIdentifier = "product-americano-hot", StockKeepingUnit = "WEGS-HOT-02", ProductName = "Hot Americano", Category = ProductCategories.HotCoffee, SellingPrice = 105m, CostPrice = 28m, StockQuantity = 38, LowStockThreshold = 10, MeasurementUnit = "cups", AllowDrinkModifiers = true },
            new() { ProductIdentifier = "product-iced-spanish-latte", StockKeepingUnit = "WEGS-ICE-01", ProductName = "Iced Spanish Latte", Category = ProductCategories.IcedCoffee, SellingPrice = 150m, CostPrice = 48m, StockQuantity = 35, LowStockThreshold = 10, MeasurementUnit = "cups", AllowDrinkModifiers = true },
            new() { ProductIdentifier = "product-iced-caramel-macchiato", StockKeepingUnit = "WEGS-ICE-02", ProductName = "Iced Caramel Macchiato", Category = ProductCategories.IcedCoffee, SellingPrice = 155m, CostPrice = 52m, StockQuantity = 8, LowStockThreshold = 10, MeasurementUnit = "cups", AllowDrinkModifiers = true },
            new() { ProductIdentifier = "product-dirty-matcha", StockKeepingUnit = "WEGS-NCF-01", ProductName = "Iced Matcha Latte", Category = ProductCategories.NonCoffee, SellingPrice = 160m, CostPrice = 58m, StockQuantity = 25, LowStockThreshold = 10, MeasurementUnit = "cups", AllowDrinkModifiers = true },
            new() { ProductIdentifier = "product-hot-chocolate", StockKeepingUnit = "WEGS-NCF-02", ProductName = "Belgian Hot Chocolate", Category = ProductCategories.NonCoffee, SellingPrice = 135m, CostPrice = 44m, StockQuantity = 20, LowStockThreshold = 8, MeasurementUnit = "cups", AllowDrinkModifiers = true },
            new() { ProductIdentifier = "product-butter-croissant", StockKeepingUnit = "WEGS-PAS-01", ProductName = "Butter Croissant", Category = ProductCategories.Pastries, SellingPrice = 95m, CostPrice = 35m, StockQuantity = 5, LowStockThreshold = 8, MeasurementUnit = "pieces", AllowDrinkModifiers = false },
            new() { ProductIdentifier = "product-basque-cheesecake", StockKeepingUnit = "WEGS-PAS-02", ProductName = "Burnt Basque Cheesecake", Category = ProductCategories.Pastries, SellingPrice = 175m, CostPrice = 65m, StockQuantity = 14, LowStockThreshold = 5, MeasurementUnit = "slices", AllowDrinkModifiers = false },
            new() { ProductIdentifier = "product-truffle-pasta", StockKeepingUnit = "WEGS-MEL-01", ProductName = "Truffle Cream Pasta", Category = ProductCategories.Meals, SellingPrice = 220m, CostPrice = 85m, StockQuantity = 16, LowStockThreshold = 5, MeasurementUnit = "servings", AllowDrinkModifiers = false },
            new() { ProductIdentifier = "product-beef-tapa", StockKeepingUnit = "WEGS-MEL-02", ProductName = "Wegs Beef Tapa Bowl", Category = ProductCategories.Meals, SellingPrice = 195m, CostPrice = 75m, StockQuantity = 18, LowStockThreshold = 5, MeasurementUnit = "servings", AllowDrinkModifiers = false },
            new() { ProductIdentifier = "product-oat-milk", StockKeepingUnit = "WEGS-ADD-01", ProductName = "Oat Milk Upgrade", Category = ProductCategories.AddOns, SellingPrice = 35m, CostPrice = 12m, StockQuantity = 50, LowStockThreshold = 10, MeasurementUnit = "servings", AllowDrinkModifiers = false }
        };

        await JsonStorageFile.WriteListAsync(_productsFilePath, seededProducts).ConfigureAwait(false);
        _cachedProducts = seededProducts;
        return _cachedProducts;
    }

    private async Task<List<Order>> EnsureOrdersSeededAsync()
    {
        if (_cachedOrders != null) return _cachedOrders;

        if (File.Exists(_ordersFilePath))
        {
            var existing = await JsonStorageFile.ReadListAsync<Order>(_ordersFilePath).ConfigureAwait(false);
            if (existing.Count > 0)
            {
                _cachedOrders = existing;
                return _cachedOrders;
            }
        }

        var today = DateTime.Now.Date;
        var sampleOrder = new Order
        {
            OrderIdentifier = Guid.NewGuid().ToString("N"),
            ReceiptNumber = $"WEGS-{today:yyyyMMdd}-0001",
            OrderType = OrderTypes.DineIn,
            CustomerOrTableLabel = "Table 2",
            PreparationStatus = OrderPreparationStatuses.Preparing,
            OrderItems = new List<OrderItem>
            {
                new()
                {
                    ProductIdentifier = "product-iced-spanish-latte",
                    ProductName = "Iced Spanish Latte",
                    Quantity = 2,
                    UnitPrice = 150m,
                    LineSubtotal = 300m,
                    CupSize = "Regular",
                    IceLevel = "Less Ice",
                    SweetnessLevel = "75%"
                }
            },
            SubtotalAmount = 300m,
            DiscountAmount = 0m,
            TotalAmount = 300m,
            PaymentMethod = PaymentMethods.Cash,
            AmountTendered = 300m,
            ChangeDue = 0m,
            OrderStatus = OrderStatuses.Completed,
            BaristaIdentifier = "owner-0001",
            BaristaName = "Wegs Cafe Owner",
            CreatedAt = IsoTimestamp.From(today.AddHours(9).AddMinutes(15))
        };

        var seededOrders = new List<Order> { sampleOrder };
        await JsonStorageFile.WriteListAsync(_ordersFilePath, seededOrders).ConfigureAwait(false);
        _cachedOrders = seededOrders;
        return _cachedOrders;
    }
}
