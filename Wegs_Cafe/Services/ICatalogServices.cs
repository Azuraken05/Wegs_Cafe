using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

public interface IProductService
{
    /// <summary>All products. CostPrice is zeroed for roles that may not see profit data.</summary>
    Task<IReadOnlyList<Product>> GetProductsAsync();
    /// <summary>Creates (ProductId empty) or updates a product. Stock of an existing product is never changed here.</summary>
    Task<Product> SaveProductAsync(Product product);
    Task DeleteProductAsync(string productId);
}

public interface IInventoryService
{
    /// <summary>Raised (on the UI thread) whenever product stock changes: sale, restock, void, edit.</summary>
    event EventHandler? StockChanged;

    /// <summary>
    /// Applies a signed stock change, writes an inventory log and an audit log.
    /// Restock: quantity &gt; 0, Spoilage: &lt; 0, Manual Adjustment: any non-zero value.
    /// </summary>
    Task<Product> AdjustStockAsync(string productId, string changeType, int quantityChanged);
    Task<IReadOnlyList<InventoryLog>> GetLogsAsync();
}

/// <summary>Stores product photos. Today: device storage. Later: Firebase Storage (returns the public URL).</summary>
public interface IImageStorageService
{
    Task<string> SaveProductImageAsync(Stream image, string fileName);
    Task DeleteAsync(string imageUrl);
}

public interface IDialogService
{
    Task AlertAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "Cancel");
    Task<string?> PromptAsync(string title, string message, string placeholder = "", bool numeric = false, string initial = "");
    Task<string?> ActionSheetAsync(string title, string cancel, params string[] buttons);
}

