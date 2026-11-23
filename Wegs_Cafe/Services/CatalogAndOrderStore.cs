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
    private readonly string _ingredientsFilePath = Path.Combine(FileSystem.AppDataDirectory, "ingredients_v3.json");
    private readonly string _inventoryLogsFilePath = Path.Combine(FileSystem.AppDataDirectory, "inventory_logs_v3.json");
    private readonly string _ordersFilePath = Path.Combine(FileSystem.AppDataDirectory, "orders_v3.json");

    private List<Product>? _cachedProducts;
    private List<Ingredient>? _cachedIngredients;
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

    // ================= RAW INGREDIENTS =================

    public async Task<IReadOnlyList<Ingredient>> GetIngredientsAsync()
    {
        RequireAuthenticated();
        await _mutex.WaitAsync().ConfigureAwait(false);
        try
        {
            var list = await EnsureIngredientsSeededAsync().ConfigureAwait(false);
            return list
                .OrderBy(ing => ing.Category)
                .ThenBy(ing => ing.IngredientName)
                .Select(ing => ing.Clone())
                .ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<Ingredient> SaveIngredientAsync(Ingredient inputIngredient)
    {
        if (!_sessionService.CanRestockInventory && !_sessionService.CanRegisterProducts)
            throw new UnauthorizedAccessException("You do not have permission to register or edit ingredients.");

        Ingredient savedIngredient;
        string? auditAction = null;
        string? auditDetails = null;

        await _mutex.WaitAsync().ConfigureAwait(false);
        try
        {
            var ingredients = await EnsureIngredientsSeededAsync().ConfigureAwait(false);
            var existing = ingredients.FirstOrDefault(i =>
                !string.IsNullOrWhiteSpace(inputIngredient.IngredientIdentifier) &&
                i.IngredientIdentifier == inputIngredient.IngredientIdentifier);

            var existingSkus = ingredients
                .Where(i => i.IngredientIdentifier != existing?.IngredientIdentifier)
                .Select(i => i.StockKeepingUnit);

            var validation = ValidationRules.ValidateIngredientRegistration(
                inputIngredient.IngredientName,
                inputIngredient.StockKeepingUnit,
                inputIngredient.Category,
                inputIngredient.MeasurementUnit,
                inputIngredient.CurrentStockQuantity.ToString(CultureInfo.InvariantCulture),
                inputIngredient.LowStockThreshold.ToString(CultureInfo.InvariantCulture),
                inputIngredient.UnitCost.ToString(CultureInfo.InvariantCulture),
                existingSkus);

            if (!validation.IsValid)
                throw new ValidationException(validation.FirstErrorOrEmpty);

            if (existing != null)
            {
                existing.IngredientName = inputIngredient.IngredientName.Trim();
                existing.StockKeepingUnit = inputIngredient.StockKeepingUnit.Trim().ToUpperInvariant();
                existing.Category = inputIngredient.Category;
                existing.MeasurementUnit = inputIngredient.MeasurementUnit;
                existing.CurrentStockQuantity = inputIngredient.CurrentStockQuantity;
                existing.LowStockThreshold = inputIngredient.LowStockThreshold;
                existing.UnitCost = inputIngredient.UnitCost;
                existing.UpdatedAt = IsoTimestamp.Now();
                savedIngredient = existing.Clone();

                auditAction = AuditActionTypes.UpdateProduct;
                auditDetails = $"Updated ingredient '{savedIngredient.IngredientName}' ({savedIngredient.StockKeepingUnit}).";
            }
            else
            {
                var newIngredient = inputIngredient.Clone();
                if (string.IsNullOrWhiteSpace(newIngredient.IngredientIdentifier))
                    newIngredient.IngredientIdentifier = $"ing-{Guid.NewGuid():N}";
                newIngredient.StockKeepingUnit = newIngredient.StockKeepingUnit.Trim().ToUpperInvariant();
                newIngredient.IngredientName = newIngredient.IngredientName.Trim();
                newIngredient.UpdatedAt = IsoTimestamp.Now();

                ingredients.Add(newIngredient);
                savedIngredient = newIngredient.Clone();

                auditAction = AuditActionTypes.CreateProduct;
                auditDetails = $"Registered new ingredient '{savedIngredient.IngredientName}' ({savedIngredient.StockKeepingUnit}).";
            }

            await JsonStorageFile.WriteListAsync(_ingredientsFilePath, ingredients).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }

        if (auditAction != null && auditDetails != null)
        {
            await _auditTrailService.RecordAuditEntryAsync(
                auditAction,
                $"ingredients/{savedIngredient.IngredientIdentifier}",
                auditDetails);
        }

        RaiseInventoryChanged();
        await SyncToFirebaseAsync(DatabaseNodes.Ingredients, savedIngredient.IngredientIdentifier, savedIngredient);
        return savedIngredient;
    }

    public async Task<Ingredient> AdjustIngredientStockAsync(
        string ingredientIdentifier,
        decimal quantityDelta,
        string movementType,
        string remarks)
    {
        if (!_sessionService.CanRestockInventory)
            throw new UnauthorizedAccessException("You do not have permission to restock or adjust inventory.");

        Ingredient updatedIngredient;
        decimal previousQuantity;
        decimal newQuantity;

        await _mutex.WaitAsync().ConfigureAwait(false);
        try
        {
            var ingredients = await EnsureIngredientsSeededAsync().ConfigureAwait(false);
            var ingredient = ingredients.FirstOrDefault(i => i.IngredientIdentifier == ingredientIdentifier)
                             ?? throw new ValidationException("Ingredient was not found.");

            var validation = ValidationRules.ValidateInventoryAdjustment(
                ingredient.CurrentStockQuantity,
                movementType,
                quantityDelta.ToString(CultureInfo.InvariantCulture),
                remarks);

            if (!validation.IsValid)
                throw new ValidationException(validation.FirstErrorOrEmpty);

            previousQuantity = ingredient.CurrentStockQuantity;
            newQuantity = previousQuantity + quantityDelta;

            ingredient.CurrentStockQuantity = newQuantity;
            ingredient.UpdatedAt = IsoTimestamp.Now();
            updatedIngredient = ingredient.Clone();

            await JsonStorageFile.WriteListAsync(_ingredientsFilePath, ingredients).ConfigureAwait(false);

            var logs = _cachedInventoryLogs ??= await JsonStorageFile.ReadListAsync<InventoryLog>(_inventoryLogsFilePath).ConfigureAwait(false);
            var log = new InventoryLog
            {
                InventoryLogIdentifier = Guid.NewGuid().ToString("N"),
                IngredientIdentifier = ingredient.IngredientIdentifier,
                IngredientName = ingredient.IngredientName,
                MeasurementUnit = ingredient.MeasurementUnit,
                MovementType = movementType,
                QuantityChanged = quantityDelta,
                PreviousStockQuantity = previousQuantity,
                NewStockQuantity = newQuantity,
                Remarks = remarks?.Trim() ?? string.Empty,
                PerformedByIdentifier = _sessionService.CurrentUser?.UserIdentifier ?? "system",
                PerformedByName = _sessionService.CurrentUser?.FullName ?? "System",
                RecordedAt = IsoTimestamp.Now()
            };
            logs.Add(log);
            await JsonStorageFile.WriteListAsync(_inventoryLogsFilePath, logs).ConfigureAwait(false);
            await SyncToFirebaseAsync(DatabaseNodes.InventoryLogs, log.InventoryLogIdentifier, log);
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
            $"ingredients/{ingredientIdentifier}",
            $"{movementType}: '{updatedIngredient.IngredientName}' stock changed from {previousQuantity:0.##} to {newQuantity:0.##} ({quantityDelta:+#.##;-#.##;0} {updatedIngredient.MeasurementUnit}).");

        RaiseInventoryChanged();
        await SyncToFirebaseAsync(DatabaseNodes.Ingredients, updatedIngredient.IngredientIdentifier, updatedIngredient);
        return updatedIngredient;
    }

    public async Task DeleteIngredientAsync(string ingredientIdentifier)
    {
        if (!_sessionService.CanRegisterProducts)
            throw new UnauthorizedAccessException("You do not have permission to delete ingredients.");

        string? deletedName = null;
        await _mutex.WaitAsync().ConfigureAwait(false);
        try
        {
            var ingredients = await EnsureIngredientsSeededAsync().ConfigureAwait(false);
            var existing = ingredients.FirstOrDefault(i => i.IngredientIdentifier == ingredientIdentifier)
                           ?? throw new ValidationException("Ingredient was not found.");

            deletedName = existing.IngredientName;
            ingredients.Remove(existing);
            await JsonStorageFile.WriteListAsync(_ingredientsFilePath, ingredients).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }

        if (deletedName != null)
        {
            await _auditTrailService.RecordAuditEntryAsync(
                AuditActionTypes.DeleteProduct,
                $"ingredients/{ingredientIdentifier}",
                $"Deleted ingredient '{deletedName}' ({ingredientIdentifier}).");
        }

        RaiseInventoryChanged();
        await DeleteFromFirebaseAsync(DatabaseNodes.Ingredients, ingredientIdentifier);
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

            var ingredients = await EnsureIngredientsSeededAsync().ConfigureAwait(false);
            var inventoryLogs = _cachedInventoryLogs ??= await JsonStorageFile.ReadListAsync<InventoryLog>(_inventoryLogsFilePath).ConfigureAwait(false);

            // 1. Finished Product Stock Deductions
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
                    MeasurementUnit = product.MeasurementUnit,
                    MovementType = InventoryMovementTypes.SaleRecipeDeduction,
                    QuantityChanged = -quantitySold,
                    PreviousStockQuantity = previousStock,
                    NewStockQuantity = product.StockQuantity,
                    ReferenceReceiptNumber = completedOrder.ReceiptNumber,
                    Remarks = $"POS Sale Order {completedOrder.ReceiptNumber}",
                    PerformedByIdentifier = _sessionService.CurrentUser!.UserIdentifier,
                    PerformedByName = _sessionService.CurrentUser!.FullName,
                    RecordedAt = IsoTimestamp.Now()
                });
            }

            // 2. Atomic Recipe Ingredients Deduction
            var rawDeductions = new Dictionary<string, (Ingredient Ingredient, decimal Quantity)>();
            foreach (var cartLine in request.OrderItems)
            {
                var product = products.FirstOrDefault(p => p.ProductIdentifier == cartLine.Product.ProductIdentifier);
                if (product?.RecipeIngredients != null)
                {
                    foreach (var recipeItem in product.RecipeIngredients)
                    {
                        var ing = ingredients.FirstOrDefault(i => i.IngredientIdentifier == recipeItem.IngredientIdentifier);
                        if (ing != null)
                        {
                            var needed = recipeItem.QuantityRequired * cartLine.Quantity;
                            if (rawDeductions.TryGetValue(ing.IngredientIdentifier, out var existingDeduction))
                            {
                                rawDeductions[ing.IngredientIdentifier] = (ing, existingDeduction.Quantity + needed);
                            }
                            else
                            {
                                rawDeductions[ing.IngredientIdentifier] = (ing, needed);
                            }
                        }
                    }
                }
            }

            foreach (var (_, (ing, qtyNeeded)) in rawDeductions)
            {
                var prevStock = ing.CurrentStockQuantity;
                ing.CurrentStockQuantity = Math.Max(0m, prevStock - qtyNeeded);
                ing.UpdatedAt = IsoTimestamp.Now();

                inventoryLogs.Add(new InventoryLog
                {
                    InventoryLogIdentifier = Guid.NewGuid().ToString("N"),
                    IngredientIdentifier = ing.IngredientIdentifier,
                    IngredientName = ing.IngredientName,
                    MeasurementUnit = ing.MeasurementUnit,
                    MovementType = InventoryMovementTypes.SaleRecipeDeduction,
                    QuantityChanged = -qtyNeeded,
                    PreviousStockQuantity = prevStock,
                    NewStockQuantity = ing.CurrentStockQuantity,
                    ReferenceReceiptNumber = completedOrder.ReceiptNumber,
                    Remarks = $"Recipe deduction for Order {completedOrder.ReceiptNumber}",
                    PerformedByIdentifier = _sessionService.CurrentUser!.UserIdentifier,
                    PerformedByName = _sessionService.CurrentUser!.FullName,
                    RecordedAt = IsoTimestamp.Now()
                });

                await SyncToFirebaseAsync(DatabaseNodes.Ingredients, ing.IngredientIdentifier, ing);
            }

            await JsonStorageFile.WriteListAsync(_ordersFilePath, orders);
            await JsonStorageFile.WriteListAsync(_productsFilePath, products);
            await JsonStorageFile.WriteListAsync(_ingredientsFilePath, ingredients);
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

    public Task<Order> VoidOrderAsync(string orderIdentifier, string securityPin)
        => VoidOrderAsync(orderIdentifier, securityPin, "Voided order transaction");

    public async Task<Order> VoidOrderAsync(string orderIdentifier, string securityPin, string voidRemarks)
    {
        RequireAuthenticated();

        var validation = ValidationRules.ValidateVoidOrder(securityPin, voidRemarks);
        if (!validation.IsValid)
            throw new ValidationException(validation.FirstErrorOrEmpty);

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
            var ingredients = await EnsureIngredientsSeededAsync();
            var inventoryLogs = _cachedInventoryLogs ??= await JsonStorageFile.ReadListAsync<InventoryLog>(_inventoryLogsFilePath).ConfigureAwait(false);

            // 1. Restore Finished Product Quantities
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
                    MeasurementUnit = product.MeasurementUnit,
                    MovementType = InventoryMovementTypes.VoidRestoration,
                    QuantityChanged = item.Quantity,
                    PreviousStockQuantity = previousStock,
                    NewStockQuantity = product.StockQuantity,
                    ReferenceReceiptNumber = targetOrder.ReceiptNumber,
                    Remarks = $"Voided Order {targetOrder.ReceiptNumber}: {voidRemarks.Trim()}",
                    PerformedByIdentifier = authorizer.UserIdentifier,
                    PerformedByName = authorizer.FullName,
                    RecordedAt = IsoTimestamp.Now()
                });

                // 2. Restore Raw Ingredients consumed by this order's recipe
                if (product.RecipeIngredients != null)
                {
                    foreach (var recipeItem in product.RecipeIngredients)
                    {
                        var ing = ingredients.FirstOrDefault(i => i.IngredientIdentifier == recipeItem.IngredientIdentifier);
                        if (ing != null)
                        {
                            var qtyToRestore = recipeItem.QuantityRequired * item.Quantity;
                            var prevIngStock = ing.CurrentStockQuantity;
                            ing.CurrentStockQuantity = prevIngStock + qtyToRestore;
                            ing.UpdatedAt = IsoTimestamp.Now();

                            inventoryLogs.Add(new InventoryLog
                            {
                                InventoryLogIdentifier = Guid.NewGuid().ToString("N"),
                                IngredientIdentifier = ing.IngredientIdentifier,
                                IngredientName = ing.IngredientName,
                                MeasurementUnit = ing.MeasurementUnit,
                                MovementType = InventoryMovementTypes.VoidRestoration,
                                QuantityChanged = qtyToRestore,
                                PreviousStockQuantity = prevIngStock,
                                NewStockQuantity = ing.CurrentStockQuantity,
                                ReferenceReceiptNumber = targetOrder.ReceiptNumber,
                                Remarks = $"Restored from voided order {targetOrder.ReceiptNumber}. Reason: {voidRemarks.Trim()}",
                                PerformedByIdentifier = authorizer.UserIdentifier,
                                PerformedByName = authorizer.FullName,
                                RecordedAt = IsoTimestamp.Now()
                            });

                            await SyncToFirebaseAsync(DatabaseNodes.Ingredients, ing.IngredientIdentifier, ing);
                        }
                    }
                }
            }

            targetOrder.OrderStatus = OrderStatuses.Voided;
            targetOrder.PreparationStatus = OrderStatuses.Voided;
            targetOrder.VoidedAt = IsoTimestamp.Now();
            targetOrder.VoidedByIdentifier = authorizer.UserIdentifier;
            targetOrder.VoidedByName = authorizer.FullName;
            targetOrder.VoidRemarks = voidRemarks.Trim();
            voidedOrder = targetOrder;

            await JsonStorageFile.WriteListAsync(_ordersFilePath, orders);
            await JsonStorageFile.WriteListAsync(_productsFilePath, products);
            await JsonStorageFile.WriteListAsync(_ingredientsFilePath, ingredients);
            await JsonStorageFile.WriteListAsync(_inventoryLogsFilePath, inventoryLogs);
        }
        finally
        {
            _mutex.Release();
        }

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.VoidOrder,
            $"orders/{voidedOrder.OrderIdentifier}",
            $"Voided order {voidedOrder.ReceiptNumber} (₱{voidedOrder.TotalAmount:N2}) and restored recipe stock. Reason: '{voidRemarks.Trim()}'. Authorized by {authorizer.FullName} ({authorizer.RoleDisplayName}).");

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
            new()
            {
                ProductIdentifier = "product-spanish-latte-hot",
                StockKeepingUnit = "WEGS-HOT-01",
                ProductName = "Hot Spanish Latte",
                Category = ProductCategories.HotCoffee,
                SellingPrice = 140m,
                CostPrice = 37.40m,
                CalculatedRecipeCost = 37.40m,
                StockQuantity = 42,
                LowStockThreshold = 10,
                MeasurementUnit = "cups",
                AllowDrinkModifiers = true,
                RecipeIngredients = new List<RecipeIngredientItem>
                {
                    new() { IngredientIdentifier = "ing-fresh-milk", IngredientName = "Fresh Milk", QuantityRequired = 180m, MeasurementUnit = "ml" },
                    new() { IngredientIdentifier = "ing-espresso-beans", IngredientName = "Espresso Beans", QuantityRequired = 18m, MeasurementUnit = "g" },
                    new() { IngredientIdentifier = "ing-condensed-milk", IngredientName = "Condensed Milk", QuantityRequired = 25m, MeasurementUnit = "ml" },
                    new() { IngredientIdentifier = "ing-16oz-cups", IngredientName = "16oz Plastic Cups", QuantityRequired = 1m, MeasurementUnit = "pcs" },
                    new() { IngredientIdentifier = "ing-cup-lids", IngredientName = "Flat Cup Lids", QuantityRequired = 1m, MeasurementUnit = "pcs" }
                }
            },
            new()
            {
                ProductIdentifier = "product-americano-hot",
                StockKeepingUnit = "WEGS-HOT-02",
                ProductName = "Hot Americano",
                Category = ProductCategories.HotCoffee,
                SellingPrice = 105m,
                CostPrice = 18.80m,
                CalculatedRecipeCost = 18.80m,
                StockQuantity = 38,
                LowStockThreshold = 10,
                MeasurementUnit = "cups",
                AllowDrinkModifiers = true,
                RecipeIngredients = new List<RecipeIngredientItem>
                {
                    new() { IngredientIdentifier = "ing-espresso-beans", IngredientName = "Espresso Beans", QuantityRequired = 18m, MeasurementUnit = "g" },
                    new() { IngredientIdentifier = "ing-16oz-cups", IngredientName = "16oz Plastic Cups", QuantityRequired = 1m, MeasurementUnit = "pcs" },
                    new() { IngredientIdentifier = "ing-cup-lids", IngredientName = "Flat Cup Lids", QuantityRequired = 1m, MeasurementUnit = "pcs" }
                }
            },
            new()
            {
                ProductIdentifier = "product-iced-spanish-latte",
                StockKeepingUnit = "WEGS-ICE-01",
                ProductName = "Iced Spanish Latte",
                Category = ProductCategories.IcedCoffee,
                SellingPrice = 150m,
                CostPrice = 38.90m,
                CalculatedRecipeCost = 38.90m,
                StockQuantity = 35,
                LowStockThreshold = 10,
                MeasurementUnit = "cups",
                AllowDrinkModifiers = true,
                RecipeIngredients = new List<RecipeIngredientItem>
                {
                    new() { IngredientIdentifier = "ing-fresh-milk", IngredientName = "Fresh Milk", QuantityRequired = 200m, MeasurementUnit = "ml" },
                    new() { IngredientIdentifier = "ing-espresso-beans", IngredientName = "Espresso Beans", QuantityRequired = 18m, MeasurementUnit = "g" },
                    new() { IngredientIdentifier = "ing-condensed-milk", IngredientName = "Condensed Milk", QuantityRequired = 30m, MeasurementUnit = "ml" },
                    new() { IngredientIdentifier = "ing-16oz-cups", IngredientName = "16oz Plastic Cups", QuantityRequired = 1m, MeasurementUnit = "pcs" },
                    new() { IngredientIdentifier = "ing-cup-lids", IngredientName = "Flat Cup Lids", QuantityRequired = 1m, MeasurementUnit = "pcs" },
                    new() { IngredientIdentifier = "ing-straws", IngredientName = "Eco Paper Straws", QuantityRequired = 1m, MeasurementUnit = "pcs" }
                }
            },
            new()
            {
                ProductIdentifier = "product-iced-caramel-macchiato",
                StockKeepingUnit = "WEGS-ICE-02",
                ProductName = "Iced Caramel Macchiato",
                Category = ProductCategories.IcedCoffee,
                SellingPrice = 155m,
                CostPrice = 40.70m,
                CalculatedRecipeCost = 40.70m,
                StockQuantity = 8,
                LowStockThreshold = 10,
                MeasurementUnit = "cups",
                AllowDrinkModifiers = true,
                RecipeIngredients = new List<RecipeIngredientItem>
                {
                    new() { IngredientIdentifier = "ing-fresh-milk", IngredientName = "Fresh Milk", QuantityRequired = 200m, MeasurementUnit = "ml" },
                    new() { IngredientIdentifier = "ing-espresso-beans", IngredientName = "Espresso Beans", QuantityRequired = 18m, MeasurementUnit = "g" },
                    new() { IngredientIdentifier = "ing-caramel-sauce", IngredientName = "Caramel Sauce", QuantityRequired = 30m, MeasurementUnit = "ml" },
                    new() { IngredientIdentifier = "ing-16oz-cups", IngredientName = "16oz Plastic Cups", QuantityRequired = 1m, MeasurementUnit = "pcs" },
                    new() { IngredientIdentifier = "ing-cup-lids", IngredientName = "Flat Cup Lids", QuantityRequired = 1m, MeasurementUnit = "pcs" },
                    new() { IngredientIdentifier = "ing-straws", IngredientName = "Eco Paper Straws", QuantityRequired = 1m, MeasurementUnit = "pcs" }
                }
            },
            new()
            {
                ProductIdentifier = "product-dirty-matcha",
                StockKeepingUnit = "WEGS-NCF-01",
                ProductName = "Iced Matcha Latte",
                Category = ProductCategories.NonCoffee,
                SellingPrice = 160m,
                CostPrice = 44.05m,
                CalculatedRecipeCost = 44.05m,
                StockQuantity = 25,
                LowStockThreshold = 10,
                MeasurementUnit = "cups",
                AllowDrinkModifiers = true,
                RecipeIngredients = new List<RecipeIngredientItem>
                {
                    new() { IngredientIdentifier = "ing-matcha-powder", IngredientName = "Matcha Powder", QuantityRequired = 15m, MeasurementUnit = "g" },
                    new() { IngredientIdentifier = "ing-fresh-milk", IngredientName = "Fresh Milk", QuantityRequired = 180m, MeasurementUnit = "ml" },
                    new() { IngredientIdentifier = "ing-16oz-cups", IngredientName = "16oz Plastic Cups", QuantityRequired = 1m, MeasurementUnit = "pcs" },
                    new() { IngredientIdentifier = "ing-cup-lids", IngredientName = "Flat Cup Lids", QuantityRequired = 1m, MeasurementUnit = "pcs" },
                    new() { IngredientIdentifier = "ing-straws", IngredientName = "Eco Paper Straws", QuantityRequired = 1m, MeasurementUnit = "pcs" }
                }
            },
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

    private async Task<List<Ingredient>> EnsureIngredientsSeededAsync()
    {
        if (_cachedIngredients != null) return _cachedIngredients;

        if (File.Exists(_ingredientsFilePath))
        {
            var existing = await JsonStorageFile.ReadListAsync<Ingredient>(_ingredientsFilePath).ConfigureAwait(false);
            if (existing.Count > 0)
            {
                _cachedIngredients = existing;
                return _cachedIngredients;
            }
        }

        var seededIngredients = new List<Ingredient>
        {
            new() { IngredientIdentifier = "ing-fresh-milk", StockKeepingUnit = "ING-MLK-01", IngredientName = "Fresh Milk", Category = IngredientCategories.DairyAndMilk, MeasurementUnit = MeasurementUnits.Milliliters, CurrentStockQuantity = 5000m, LowStockThreshold = 1000m, UnitCost = 0.08m },
            new() { IngredientIdentifier = "ing-espresso-beans", StockKeepingUnit = "ING-COF-01", IngredientName = "Espresso Beans", Category = IngredientCategories.CoffeeAndTea, MeasurementUnit = MeasurementUnits.Grams, CurrentStockQuantity = 3000m, LowStockThreshold = 500m, UnitCost = 0.85m },
            new() { IngredientIdentifier = "ing-condensed-milk", StockKeepingUnit = "ING-MLK-02", IngredientName = "Condensed Milk", Category = IngredientCategories.DairyAndMilk, MeasurementUnit = MeasurementUnits.Milliliters, CurrentStockQuantity = 2000m, LowStockThreshold = 400m, UnitCost = 0.12m },
            new() { IngredientIdentifier = "ing-brown-sugar-syrup", StockKeepingUnit = "ING-SYR-01", IngredientName = "Brown Sugar Syrup", Category = IngredientCategories.SyrupsAndSauces, MeasurementUnit = MeasurementUnits.Milliliters, CurrentStockQuantity = 2000m, LowStockThreshold = 400m, UnitCost = 0.15m },
            new() { IngredientIdentifier = "ing-caramel-sauce", StockKeepingUnit = "ING-SYR-02", IngredientName = "Caramel Sauce", Category = IngredientCategories.SyrupsAndSauces, MeasurementUnit = MeasurementUnits.Milliliters, CurrentStockQuantity = 1500m, LowStockThreshold = 300m, UnitCost = 0.18m },
            new() { IngredientIdentifier = "ing-matcha-powder", StockKeepingUnit = "ING-COF-02", IngredientName = "Matcha Powder", Category = IngredientCategories.CoffeeAndTea, MeasurementUnit = MeasurementUnits.Grams, CurrentStockQuantity = 800m, LowStockThreshold = 200m, UnitCost = 1.20m },
            new() { IngredientIdentifier = "ing-rock-salt", StockKeepingUnit = "ING-DRY-01", IngredientName = "Rock Salt", Category = IngredientCategories.DryGoodsAndSpices, MeasurementUnit = MeasurementUnits.Grams, CurrentStockQuantity = 1000m, LowStockThreshold = 200m, UnitCost = 0.05m },
            new() { IngredientIdentifier = "ing-16oz-cups", StockKeepingUnit = "ING-PCK-01", IngredientName = "16oz Plastic Cups", Category = IngredientCategories.PackagingAndCups, MeasurementUnit = MeasurementUnits.Pieces, CurrentStockQuantity = 500m, LowStockThreshold = 100m, UnitCost = 2.50m },
            new() { IngredientIdentifier = "ing-cup-lids", StockKeepingUnit = "ING-PCK-02", IngredientName = "Flat Cup Lids", Category = IngredientCategories.PackagingAndCups, MeasurementUnit = MeasurementUnits.Pieces, CurrentStockQuantity = 500m, LowStockThreshold = 100m, UnitCost = 1.00m },
            new() { IngredientIdentifier = "ing-straws", StockKeepingUnit = "ING-PCK-03", IngredientName = "Eco Paper Straws", Category = IngredientCategories.PackagingAndCups, MeasurementUnit = MeasurementUnits.Pieces, CurrentStockQuantity = 500m, LowStockThreshold = 100m, UnitCost = 0.50m }
        };

        await JsonStorageFile.WriteListAsync(_ingredientsFilePath, seededIngredients).ConfigureAwait(false);
        _cachedIngredients = seededIngredients;
        return _cachedIngredients;
    }
}
