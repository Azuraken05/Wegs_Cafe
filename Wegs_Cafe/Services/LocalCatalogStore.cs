using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Products + inventory logs in on-device JSON files (until Firebase).
/// One class (one lock) owns both so a stock change and its log are always written together.
/// </summary>
public partial class LocalCatalogStore : IProductService, IInventoryService
{
    private readonly SessionService _session;
    private readonly IAuditService _audit;
    private readonly IImageStorageService _images;
    private readonly IAuthService _auth;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _productsPath = Path.Combine(FileSystem.AppDataDirectory, "local_products.json");
    private readonly string _logsPath = Path.Combine(FileSystem.AppDataDirectory, "local_inventory_logs.json");

    public LocalCatalogStore(SessionService session, IAuditService audit, IImageStorageService images, IAuthService auth)
    {
        _session = session;
        _audit = audit;
        _images = images;
        _auth = auth;
    }

    public event EventHandler? StockChanged;

    private void RaiseStockChanged()
        => MainThread.BeginInvokeOnMainThread(() => StockChanged?.Invoke(this, EventArgs.Empty));

    // ---------------- Products ----------------
    public async Task<IReadOnlyList<Product>> GetProductsAsync()
    {
        RequireLogin();
        await _lock.WaitAsync();
        try
        {
            var list = await EnsureProductsSeededAsync();
            return list.OrderBy(p => p.Category).ThenBy(p => p.Name)
                       .Select(p => Visible(p)).ToList();
        }
        finally { _lock.Release(); }
    }

    private async Task<List<Product>> EnsureProductsSeededAsync()
    {
        if (File.Exists(_productsPath))
        {
            var existing = await JsonFile.ReadAsync<Product>(_productsPath);
            if (existing.Count > 0) return existing;
        }

        var seeded = new List<Product>
        {
            new() { ProductId = "prod-espresso", Sku = "COF-001", Name = "Spanish Latte", Category = ProductCategories.Coffee, Price = 140m, CostPrice = 45m, StockQuantity = 42, LowStockThreshold = 10, Unit = "cups" },
            new() { ProductId = "prod-americano", Sku = "COF-002", Name = "Iced Americano", Category = ProductCategories.Coffee, Price = 110m, CostPrice = 30m, StockQuantity = 38, LowStockThreshold = 10, Unit = "cups" },
            new() { ProductId = "prod-caramel", Sku = "COF-003", Name = "Caramel Macchiato", Category = ProductCategories.Coffee, Price = 155m, CostPrice = 52m, StockQuantity = 8, LowStockThreshold = 10, Unit = "cups" },
            new() { ProductId = "prod-matcha", Sku = "NCF-001", Name = "Dirty Matcha Latte", Category = ProductCategories.NonCoffee, Price = 160m, CostPrice = 58m, StockQuantity = 25, LowStockThreshold = 10, Unit = "cups" },
            new() { ProductId = "prod-choco", Sku = "NCF-002", Name = "Belgian Hot Chocolate", Category = ProductCategories.NonCoffee, Price = 135m, CostPrice = 44m, StockQuantity = 20, LowStockThreshold = 8, Unit = "cups" },
            new() { ProductId = "prod-croissant", Sku = "PAS-001", Name = "Butter Croissant", Category = ProductCategories.Pastries, Price = 95m, CostPrice = 35m, StockQuantity = 5, LowStockThreshold = 8, Unit = "pcs" },
            new() { ProductId = "prod-cheesecake", Sku = "PAS-002", Name = "Burnt Basque Cheesecake", Category = ProductCategories.Pastries, Price = 175m, CostPrice = 65m, StockQuantity = 14, LowStockThreshold = 5, Unit = "slices" },
            new() { ProductId = "prod-pasta", Sku = "MEL-001", Name = "Truffle Cream Pasta", Category = ProductCategories.Meals, Price = 220m, CostPrice = 85m, StockQuantity = 16, LowStockThreshold = 5, Unit = "servings" },
            new() { ProductId = "prod-tapa", Sku = "MEL-002", Name = "Wegs Beef Tapa Bowl", Category = ProductCategories.Meals, Price = 195m, CostPrice = 75m, StockQuantity = 18, LowStockThreshold = 5, Unit = "servings" },
            new() { ProductId = "prod-oatmilk", Sku = "ADD-001", Name = "Sub Oat Milk", Category = ProductCategories.AddOns, Price = 35m, CostPrice = 12m, StockQuantity = 50, LowStockThreshold = 10, Unit = "servings" }
        };
        await JsonFile.WriteAsync(_productsPath, seeded);
        return seeded;
    }

    public async Task<Product> SaveProductAsync(Product input)
    {
        if (!_session.CanManageProducts) throw new UnauthorizedAccessException("You are not allowed to edit products.");
        Validate(input);

        await _lock.WaitAsync();
        try
        {
            var list = await JsonFile.ReadAsync<Product>(_productsPath);
            var sku = input.Sku.Trim();
            var existing = list.FirstOrDefault(p => p.ProductId == input.ProductId && !string.IsNullOrEmpty(input.ProductId));

            if (list.Any(p => p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase) && p.ProductId != existing?.ProductId))
                throw new ValidationException("That SKU is already used by another product.");

            var product = input.Clone();
            product.Sku = sku;
            product.Name = input.Name.Trim();
            product.UpdatedAt = IsoDate.Now();

            if (existing == null)
            {
                product.ProductId = Guid.NewGuid().ToString("N");
                list.Add(product);
                await JsonFile.WriteAsync(_productsPath, list);

                await _audit.LogAsync(AuditActions.CreateProduct, $"products/{product.ProductId}",
                    $"Registered \"{product.Name}\" ({product.Sku}) at ₱{product.Price:N2}, stock {product.StockQuantity}.");

                if (product.StockQuantity > 0)
                    await AppendLogAsync(product, InventoryChangeTypes.ManualAdjustment, product.StockQuantity, 0, product.StockQuantity);
            }
            else
            {
                // Stock only changes through AdjustStockAsync. Managers can't see/change cost price.
                product.StockQuantity = existing.StockQuantity;
                if (!_session.CanViewProfit) product.CostPrice = existing.CostPrice;

                var index = list.IndexOf(existing);
                list[index] = product;
                await JsonFile.WriteAsync(_productsPath, list);

                if (existing.Price != product.Price)
                    await _audit.LogAsync(AuditActions.UpdatePrice, $"products/{product.ProductId}",
                        $"\"{product.Name}\" price ₱{existing.Price:N2} → ₱{product.Price:N2}.");
                await _audit.LogAsync(AuditActions.UpdateProduct, $"products/{product.ProductId}", $"Edited \"{product.Name}\" ({product.Sku}).");

                if (existing.ImageUrl != product.ImageUrl && !string.IsNullOrEmpty(existing.ImageUrl))
                    await _images.DeleteAsync(existing.ImageUrl);
            }
            RaiseStockChanged();
            return Visible(product);
        }
        finally { _lock.Release(); }
    }

    public async Task DeleteProductAsync(string productId)
    {
        if (!_session.CanManageProducts) throw new UnauthorizedAccessException("You are not allowed to delete products.");
        await _lock.WaitAsync();
        try
        {
            var list = await JsonFile.ReadAsync<Product>(_productsPath);
            var product = list.FirstOrDefault(p => p.ProductId == productId) ?? throw new ValidationException("Product not found.");
            list.Remove(product);
            await JsonFile.WriteAsync(_productsPath, list);
            await _audit.LogAsync(AuditActions.DeleteProduct, $"products/{productId}", $"Deleted \"{product.Name}\" ({product.Sku}).");
            if (!string.IsNullOrEmpty(product.ImageUrl)) await _images.DeleteAsync(product.ImageUrl);
            RaiseStockChanged();
        }
        finally { _lock.Release(); }
    }

    // ---------------- Inventory ----------------
    public async Task<Product> AdjustStockAsync(string productId, string changeType, int quantityChanged)
    {
        if (!_session.CanEditInventory) throw new UnauthorizedAccessException("You are not allowed to change stock.");

        if (quantityChanged == 0) throw new ValidationException("Quantity cannot be zero.");
        if (changeType == InventoryChangeTypes.Restock && quantityChanged < 0) throw new ValidationException("Restock must be a positive number.");
        if (changeType == InventoryChangeTypes.Spoilage && quantityChanged > 0) throw new ValidationException("Spoilage must remove stock.");

        await _lock.WaitAsync();
        try
        {
            var list = await JsonFile.ReadAsync<Product>(_productsPath);
            var product = list.FirstOrDefault(p => p.ProductId == productId) ?? throw new ValidationException("Product not found.");

            var previous = product.StockQuantity;
            var next = previous + quantityChanged;
            if (next < 0) throw new ValidationException($"Not enough stock. Current stock is {previous}.");

            product.StockQuantity = next;
            product.UpdatedAt = IsoDate.Now();
            await JsonFile.WriteAsync(_productsPath, list);
            await AppendLogAsync(product, changeType, quantityChanged, previous, next);

            var action = changeType == InventoryChangeTypes.Restock ? AuditActions.RestockItem : AuditActions.AdjustStock;
            await _audit.LogAsync(action, $"products/{productId}",
                $"{changeType}: \"{product.Name}\" {previous} → {next} ({quantityChanged:+#;-#;0}).");
            RaiseStockChanged();
            return Visible(product);
        }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<InventoryLog>> GetLogsAsync()
    {
        RequireLogin();
        await _lock.WaitAsync();
        try { return (await JsonFile.ReadAsync<InventoryLog>(_logsPath)).OrderByDescending(l => l.Timestamp).ToList(); }
        finally { _lock.Release(); }
    }

    private async Task AppendLogAsync(Product p, string type, int change, int previous, int next)
    {
        var logs = await JsonFile.ReadAsync<InventoryLog>(_logsPath);
        logs.Add(new InventoryLog
        {
            LogId = Guid.NewGuid().ToString("N"),
            ProductId = p.ProductId,
            ProductName = p.Name,
            ChangeType = type,
            QuantityChanged = change,
            PreviousStock = previous,
            NewStock = next,
            PerformedByUid = _session.CurrentUser?.Uid ?? "",
            PerformedByName = _session.CurrentUser?.FullName ?? ""
        });
        await JsonFile.WriteAsync(_logsPath, logs);
    }

    // ---------------- helpers ----------------
    private Product Visible(Product p)
    {
        var copy = p.Clone();
        if (!_session.CanViewProfit) copy.CostPrice = 0;   // hide margins from manager/staff
        return copy;
    }

    private void RequireLogin()
    {
        if (!_session.IsLoggedIn) throw new UnauthorizedAccessException("Please sign in.");
    }

    private static void Validate(Product p)
    {
        if (string.IsNullOrWhiteSpace(p.Name)) throw new ValidationException("Enter the product name.");
        if (string.IsNullOrWhiteSpace(p.Sku)) throw new ValidationException("Enter a SKU.");
        if (!ProductCategories.All.Contains(p.Category)) throw new ValidationException("Choose a category.");
        if (p.Price <= 0) throw new ValidationException("Selling price must be greater than zero.");
        if (p.CostPrice < 0) throw new ValidationException("Cost price cannot be negative.");
        if (p.StockQuantity < 0) throw new ValidationException("Stock cannot be negative.");
        if (p.LowStockThreshold < 0) throw new ValidationException("Low-stock threshold cannot be negative.");
        if (string.IsNullOrWhiteSpace(p.Unit)) throw new ValidationException("Choose a unit.");
    }
}

