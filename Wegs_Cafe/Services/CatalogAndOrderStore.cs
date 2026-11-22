using System.Globalization;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Atomic data store for products, inventory_logs, and orders using the full snake_case schema.
/// </summary>
public class CatalogAndOrderStore : IProductManagementService, IInventoryManagementService, IOrderTransactionService
{
    private readonly SessionService _sessionService;
    private readonly IAuditTrailService _auditTrailService;
    private readonly IImageStorageService _imageStorageService;
    private readonly IAuthenticationService _authenticationService;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    private readonly string _productsFilePath = Path.Combine(FileSystem.AppDataDirectory, "products_v2.json");
    private readonly string _inventoryLogsFilePath = Path.Combine(FileSystem.AppDataDirectory, "inventory_logs_v2.json");
    private readonly string _ordersFilePath = Path.Combine(FileSystem.AppDataDirectory, "orders_v2.json");

    public event EventHandler? InventoryChanged;

    public CatalogAndOrderStore(
        SessionService sessionService,
        IAuditTrailService auditTrailService,
        IImageStorageService imageStorageService,
        IAuthenticationService authenticationService)
    {
        _sessionService = sessionService;
        _auditTrailService = auditTrailService;
        _imageStorageService = imageStorageService;
        _authenticationService = authenticationService;
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
        if (!_sessionService.CanAccessProductRegistration)
            throw new UnauthorizedAccessException("You do not have permission to register or edit products.");

        ValidateProduct(inputProduct);

        Product savedProduct;
        string? auditAction = null;
        string? auditDetails = null;
        string? priceAuditDetails = null;

        await _mutex.WaitAsync();
        try
        {
            var products = await EnsureProductsSeededAsync();
            var cleanSku = inputProduct.StockKeepingUnit.Trim();
            var existing = products.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(inputProduct.ProductIdentifier) &&
                p.ProductIdentifier == inputProduct.ProductIdentifier);

            if (products.Any(p =>
                    p.StockKeepingUnit.Equals(cleanSku, StringComparison.OrdinalIgnoreCase) &&
                    p.ProductIdentifier != existing?.ProductIdentifier))
            {
                throw new ValidationException("Another product already uses that Stock Keeping Unit (SKU).");
            }

            savedProduct = inputProduct.Clone();
            savedProduct.StockKeepingUnit = cleanSku;
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
                        savedProduct.StockQuantity);
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
        return FilterSensitiveFields(savedProduct);
    }

    public async Task DeleteProductAsync(string productIdentifier)
    {
        if (!_sessionService.CanAccessProductRegistration)
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
    }

    // ================= INVENTORY =================

    public async Task<Product> RecordStockMovementAsync(string productIdentifier, string movementType, int quantityChanged)
    {
        if (!_sessionService.CanModifyInventoryStock)
            throw new UnauthorizedAccessException("You do not have permission to modify inventory stock.");

        if (quantityChanged == 0)
            throw new ValidationException("Quantity changed cannot be zero.");
        if (movementType == InventoryMovementTypes.StockReplenishment && quantityChanged < 0)
            throw new ValidationException("Stock replenishment quantity must be positive.");
        if (movementType == InventoryMovementTypes.SpoilageOrWaste && quantityChanged > 0)
            throw new ValidationException("Spoilage or waste quantity must be negative.");

        Product updatedProduct;
        int previousQuantity;
        int newQuantity;

        await _mutex.WaitAsync();
        try
        {
            var products = await EnsureProductsSeededAsync();
            var product = products.FirstOrDefault(p => p.ProductIdentifier == productIdentifier)
                          ?? throw new ValidationException("Product was not found.");

            previousQuantity = product.StockQuantity;
            newQuantity = previousQuantity + quantityChanged;
            if (newQuantity < 0)
                throw new ValidationException($"Insufficient stock. Current stock is {previousQuantity} {product.MeasurementUnit}.");

            product.StockQuantity = newQuantity;
            product.UpdatedAt = IsoTimestamp.Now();
            updatedProduct = product.Clone();

            await JsonStorageFile.WriteListAsync(_productsFilePath, products);
            await AppendInventoryLogInternalAsync(product, movementType, quantityChanged, previousQuantity, newQuantity);
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
        return FilterSensitiveFields(updatedProduct);
    }

    public async Task<IReadOnlyList<InventoryLog>> GetInventoryLogsAsync()
    {
        RequireAuthenticated();
        await _mutex.WaitAsync();
        try
        {
            var logs = await JsonStorageFile.ReadListAsync<InventoryLog>(_inventoryLogsFilePath);
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
                    throw new ValidationException($"'{product.ProductName}' is currently unavailable.");
                if (product.StockQuantity < cartLine.Quantity)
                    throw new ValidationException($"Insufficient stock for '{product.ProductName}'. Remaining: {product.StockQuantity} {product.MeasurementUnit}.");

                orderItems.Add(new OrderItem
                {
                    ProductIdentifier = product.ProductIdentifier,
                    ProductName = product.ProductName,
                    Quantity = cartLine.Quantity,
                    UnitPrice = product.SellingPrice,
                    LineSubtotal = product.SellingPrice * cartLine.Quantity,
                    PreparationNotes = cartLine.PreparationNotes?.Trim() ?? string.Empty
                });

                stockDeductions.Add((product, cartLine.Quantity));
            }

            var subtotalAmount = orderItems.Sum(line => line.LineSubtotal);
            var discountAmount = request.DiscountAmount;
            if (discountAmount < 0 || discountAmount > subtotalAmount)
                throw new ValidationException("Discount amount must be between ₱0.00 and the subtotal.");
            if (discountAmount > 0 && !_sessionService.CanAccessProductRegistration)
                throw new UnauthorizedAccessException("Only the Owner or a Manager can apply discounts.");

            var totalAmount = subtotalAmount - discountAmount;
            decimal amountTendered;
            decimal changeDue;
            var referenceNumber = request.PaymentReferenceNumber?.Trim() ?? string.Empty;

            switch (request.PaymentMethod)
            {
                case PaymentMethods.Cash:
                    if (request.AmountTendered < totalAmount)
                        throw new ValidationException("Amount tendered is less than the total amount due.");
                    amountTendered = request.AmountTendered;
                    changeDue = amountTendered - totalAmount;
                    referenceNumber = string.Empty;
                    break;

                case PaymentMethods.GCash:
                    if (referenceNumber.Length < 4)
                        throw new ValidationException("Please enter the mandatory GCash Reference Number.");
                    amountTendered = totalAmount;
                    changeDue = 0m;
                    break;

                case PaymentMethods.CreditCard:
                    if (referenceNumber.Length < 4)
                        throw new ValidationException("Please enter the mandatory Credit Card Approval / Trace Reference Number.");
                    amountTendered = totalAmount;
                    changeDue = 0m;
                    break;

                default:
                    throw new ValidationException("Please select a valid payment method.");
            }

            var datePrefix = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var sequenceNumber = orders.Count(o => o.ReceiptNumber.StartsWith($"WEGS-{datePrefix}-")) + 1;

            completedOrder = new Order
            {
                OrderIdentifier = Guid.NewGuid().ToString("N"),
                ReceiptNumber = $"WEGS-{datePrefix}-{sequenceNumber:0000}",
                OrderType = request.OrderType,
                CustomerOrTableLabel = request.CustomerOrTableLabel?.Trim() ?? string.Empty,
                OrderItems = orderItems,
                SubtotalAmount = subtotalAmount,
                DiscountAmount = discountAmount,
                TotalAmount = totalAmount,
                PaymentMethod = request.PaymentMethod,
                PaymentReferenceNumber = referenceNumber,
                AmountTendered = amountTendered,
                ChangeDue = changeDue,
                OrderStatus = OrderStatuses.Completed,
                CashierIdentifier = _sessionService.CurrentUser!.UserIdentifier,
                CashierName = _sessionService.CurrentUser!.FullName,
                CreatedAt = IsoTimestamp.Now()
            };

            orders.Add(completedOrder);

            var inventoryLogs = await JsonStorageFile.ReadListAsync<InventoryLog>(_inventoryLogsFilePath);
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
        return completedOrder;
    }

    public async Task<IReadOnlyList<Order>> GetOrdersAsync()
    {
        RequireAuthenticated();
        await _mutex.WaitAsync();
        try
        {
            var allOrders = await EnsureOrdersSeededAsync();
            var visibleOrders = _sessionService.IsOwner || _sessionService.IsManager
                ? allOrders
                : allOrders.Where(o => o.CashierIdentifier == _sessionService.CurrentUser!.UserIdentifier).ToList();

            return visibleOrders.OrderByDescending(o => o.CreatedAt).ToList();
        }
        finally
        {
            _mutex.Release();
        }
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
            var inventoryLogs = await JsonStorageFile.ReadListAsync<InventoryLog>(_inventoryLogsFilePath);

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
                    MovementType = InventoryMovementTypes.ManualAdjustment,
                    QuantityChanged = item.Quantity,
                    PreviousStockQuantity = previousStock,
                    NewStockQuantity = product.StockQuantity,
                    PerformedByIdentifier = authorizer.UserIdentifier,
                    PerformedByName = authorizer.FullName,
                    RecordedAt = IsoTimestamp.Now()
                });
            }

            targetOrder.OrderStatus = OrderStatuses.Voided;
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
        return voidedOrder;
    }

    // ================= SEEDING & HELPERS =================

    private async Task AppendInventoryLogInternalAsync(Product product, string movementType, int quantityChanged, int previousQuantity, int newQuantity)
    {
        var logs = await JsonStorageFile.ReadListAsync<InventoryLog>(_inventoryLogsFilePath);
        logs.Add(new InventoryLog
        {
            InventoryLogIdentifier = Guid.NewGuid().ToString("N"),
            ProductIdentifier = product.ProductIdentifier,
            ProductName = product.ProductName,
            MovementType = movementType,
            QuantityChanged = quantityChanged,
            PreviousStockQuantity = previousQuantity,
            NewStockQuantity = newQuantity,
            PerformedByIdentifier = _sessionService.CurrentUser?.UserIdentifier ?? "system",
            PerformedByName = _sessionService.CurrentUser?.FullName ?? "System",
            RecordedAt = IsoTimestamp.Now()
        });
        await JsonStorageFile.WriteListAsync(_inventoryLogsFilePath, logs);
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

    private static void ValidateProduct(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.ProductName))
            throw new ValidationException("Please enter the product name.");
        if (string.IsNullOrWhiteSpace(product.StockKeepingUnit))
            throw new ValidationException("Please enter the Stock Keeping Unit (SKU).");
        if (!ProductCategories.All.Contains(product.Category))
            throw new ValidationException("Please select a valid category.");
        if (product.SellingPrice <= 0)
            throw new ValidationException("Selling price must be greater than zero.");
        if (product.CostPrice < 0)
            throw new ValidationException("Cost price cannot be negative.");
        if (product.StockQuantity < 0)
            throw new ValidationException("Stock quantity cannot be negative.");
        if (product.LowStockThreshold < 0)
            throw new ValidationException("Low-stock threshold cannot be negative.");
        if (string.IsNullOrWhiteSpace(product.MeasurementUnit))
            throw new ValidationException("Please specify a measurement unit.");
    }

    private async Task<List<Product>> EnsureProductsSeededAsync()
    {
        if (File.Exists(_productsFilePath))
        {
            var existing = await JsonStorageFile.ReadListAsync<Product>(_productsFilePath);
            if (existing.Count > 0) return existing;
        }

        var seededProducts = new List<Product>
        {
            new() { ProductIdentifier = "product-spanish-latte", StockKeepingUnit = "COF-001", ProductName = "Spanish Latte", Category = ProductCategories.Coffee, SellingPrice = 140m, CostPrice = 45m, StockQuantity = 42, LowStockThreshold = 10, MeasurementUnit = "cups" },
            new() { ProductIdentifier = "product-iced-americano", StockKeepingUnit = "COF-002", ProductName = "Iced Americano", Category = ProductCategories.Coffee, SellingPrice = 110m, CostPrice = 30m, StockQuantity = 38, LowStockThreshold = 10, MeasurementUnit = "cups" },
            new() { ProductIdentifier = "product-caramel-macchiato", StockKeepingUnit = "COF-003", ProductName = "Caramel Macchiato", Category = ProductCategories.Coffee, SellingPrice = 155m, CostPrice = 52m, StockQuantity = 8, LowStockThreshold = 10, MeasurementUnit = "cups" },
            new() { ProductIdentifier = "product-dirty-matcha", StockKeepingUnit = "NCF-001", ProductName = "Dirty Matcha Latte", Category = ProductCategories.NonCoffee, SellingPrice = 160m, CostPrice = 58m, StockQuantity = 25, LowStockThreshold = 10, MeasurementUnit = "cups" },
            new() { ProductIdentifier = "product-hot-chocolate", StockKeepingUnit = "NCF-002", ProductName = "Belgian Hot Chocolate", Category = ProductCategories.NonCoffee, SellingPrice = 135m, CostPrice = 44m, StockQuantity = 20, LowStockThreshold = 8, MeasurementUnit = "cups" },
            new() { ProductIdentifier = "product-butter-croissant", StockKeepingUnit = "PAS-001", ProductName = "Butter Croissant", Category = ProductCategories.Pastries, SellingPrice = 95m, CostPrice = 35m, StockQuantity = 5, LowStockThreshold = 8, MeasurementUnit = "pieces" },
            new() { ProductIdentifier = "product-basque-cheesecake", StockKeepingUnit = "PAS-002", ProductName = "Burnt Basque Cheesecake", Category = ProductCategories.Pastries, SellingPrice = 175m, CostPrice = 65m, StockQuantity = 14, LowStockThreshold = 5, MeasurementUnit = "slices" },
            new() { ProductIdentifier = "product-truffle-pasta", StockKeepingUnit = "MEL-001", ProductName = "Truffle Cream Pasta", Category = ProductCategories.Meals, SellingPrice = 220m, CostPrice = 85m, StockQuantity = 16, LowStockThreshold = 5, MeasurementUnit = "servings" },
            new() { ProductIdentifier = "product-beef-tapa", StockKeepingUnit = "MEL-002", ProductName = "Wegs Beef Tapa Bowl", Category = ProductCategories.Meals, SellingPrice = 195m, CostPrice = 75m, StockQuantity = 18, LowStockThreshold = 5, MeasurementUnit = "servings" },
            new() { ProductIdentifier = "product-oat-milk", StockKeepingUnit = "ADD-001", ProductName = "Oat Milk Upgrade", Category = ProductCategories.AddOns, SellingPrice = 35m, CostPrice = 12m, StockQuantity = 50, LowStockThreshold = 10, MeasurementUnit = "servings" }
        };

        await JsonStorageFile.WriteListAsync(_productsFilePath, seededProducts);
        return seededProducts;
    }

    private async Task<List<Order>> EnsureOrdersSeededAsync()
    {
        if (File.Exists(_ordersFilePath))
        {
            var existing = await JsonStorageFile.ReadListAsync<Order>(_ordersFilePath);
            if (existing.Count > 0) return existing;
        }

        Order BuildSampleOrder(
            int sequence,
            DateTime localTimestamp,
            string orderType,
            string customerOrTable,
            string paymentMethod,
            string referenceNumber,
            params (string productId, string productName, int quantity, decimal unitPrice)[] lines)
        {
            var orderItems = lines.Select(line => new OrderItem
            {
                ProductIdentifier = line.productId,
                ProductName = line.productName,
                Quantity = line.quantity,
                UnitPrice = line.unitPrice,
                LineSubtotal = line.quantity * line.unitPrice,
                PreparationNotes = string.Empty
            }).ToList();

            var subtotal = orderItems.Sum(i => i.LineSubtotal);
            return new Order
            {
                OrderIdentifier = Guid.NewGuid().ToString("N"),
                ReceiptNumber = $"WEGS-{localTimestamp:yyyyMMdd}-{sequence:0000}",
                OrderType = orderType,
                CustomerOrTableLabel = customerOrTable,
                OrderItems = orderItems,
                SubtotalAmount = subtotal,
                DiscountAmount = 0m,
                TotalAmount = subtotal,
                PaymentMethod = paymentMethod,
                PaymentReferenceNumber = referenceNumber,
                AmountTendered = subtotal,
                ChangeDue = 0m,
                OrderStatus = OrderStatuses.Completed,
                CashierIdentifier = "owner-0001",
                CashierName = "Weg's Cafe Owner",
                CreatedAt = IsoTimestamp.From(localTimestamp)
            };
        }

        var today = DateTime.Now.Date;
        var seededOrders = new List<Order>
        {
            BuildSampleOrder(1, today.AddHours(8).AddMinutes(15), OrderTypes.DineIn, "Table 2", PaymentMethods.Cash, "",
                ("product-spanish-latte", "Spanish Latte", 2, 140m),
                ("product-butter-croissant", "Butter Croissant", 2, 95m)),
            BuildSampleOrder(2, today.AddHours(9).AddMinutes(40), OrderTypes.TakeOut, "Walk-in Customer", PaymentMethods.GCash, "901284112",
                ("product-iced-americano", "Iced Americano", 3, 110m),
                ("product-oat-milk", "Oat Milk Upgrade", 1, 35m)),
            BuildSampleOrder(3, today.AddHours(11).AddMinutes(20), OrderTypes.DineIn, "Table 5", PaymentMethods.CreditCard, "482910",
                ("product-truffle-pasta", "Truffle Cream Pasta", 2, 220m),
                ("product-dirty-matcha", "Dirty Matcha Latte", 2, 160m)),
            BuildSampleOrder(4, today.AddHours(13).AddMinutes(5), OrderTypes.DineIn, "Table 1", PaymentMethods.Cash, "",
                ("product-beef-tapa", "Wegs Beef Tapa Bowl", 2, 195m),
                ("product-caramel-macchiato", "Caramel Macchiato", 2, 155m)),
            BuildSampleOrder(5, today.AddHours(15).AddMinutes(30), OrderTypes.TakeOut, "Take-Out Counter", PaymentMethods.GCash, "901299871",
                ("product-basque-cheesecake", "Burnt Basque Cheesecake", 2, 175m),
                ("product-spanish-latte", "Spanish Latte", 1, 140m)),
            BuildSampleOrder(1, today.AddDays(-1).AddHours(10), OrderTypes.DineIn, "Table 3", PaymentMethods.Cash, "",
                ("product-spanish-latte", "Spanish Latte", 3, 140m),
                ("product-truffle-pasta", "Truffle Cream Pasta", 1, 220m)),
            BuildSampleOrder(2, today.AddDays(-1).AddHours(14), OrderTypes.TakeOut, "Walk-in Customer", PaymentMethods.GCash, "881920114",
                ("product-dirty-matcha", "Dirty Matcha Latte", 2, 160m),
                ("product-butter-croissant", "Butter Croissant", 3, 95m)),
            BuildSampleOrder(1, today.AddDays(-2).AddHours(11), OrderTypes.DineIn, "Table 4", PaymentMethods.CreditCard, "771209",
                ("product-beef-tapa", "Wegs Beef Tapa Bowl", 3, 195m),
                ("product-iced-americano", "Iced Americano", 3, 110m)),
            BuildSampleOrder(1, today.AddDays(-3).AddHours(16), OrderTypes.DineIn, "Table 2", PaymentMethods.Cash, "",
                ("product-caramel-macchiato", "Caramel Macchiato", 4, 155m),
                ("product-basque-cheesecake", "Burnt Basque Cheesecake", 2, 175m))
        };

        await JsonStorageFile.WriteListAsync(_ordersFilePath, seededOrders);
        return seededOrders;
    }
}
