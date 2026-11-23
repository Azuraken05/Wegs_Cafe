using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

public class AuthenticationException : Exception
{
    public AuthenticationException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

public class CheckoutTransactionRequest
{
    public string OrderType { get; set; } = OrderTypes.DineIn;
    public string CustomerOrTableLabel { get; set; } = string.Empty;
    public List<OrderCartItem> OrderItems { get; set; } = new();
    public decimal DiscountAmount { get; set; }
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;
    public string PaymentReferenceNumber { get; set; } = string.Empty;
    public decimal AmountTendered { get; set; }
}

public record BluetoothPrinterDevice(string DeviceName, string DeviceAddress);

public interface IAuthenticationService
{
    Task<UserAccount> SignInAsync(string usernameOrEmail, string password);
    Task<UserAccount> SignInWithPinAsync(string securityPin);
    Task SignOutAsync();
    Task<UserAccount> AuthorizeSecurityPinAsync(string securityPin);
}

public interface IUserManagementService
{
    Task<IReadOnlyList<UserAccount>> GetUserAccountsAsync();
    Task<UserAccount> RegisterStaffAccountAsync(
        string fullName,
        string emailAddress,
        string password,
        string securityPin,
        string roleName,
        UserPermissions permissions);
    Task SetAccountActiveStatusAsync(string userIdentifier, bool isActive);
}

public interface IProductManagementService
{
    Task<IReadOnlyList<Product>> GetProductsAsync();
    Task<Product> SaveProductAsync(Product product);
    Task<Product> ToggleProductAvailabilityAsync(string productIdentifier, bool isAvailable);
    Task DeleteProductAsync(string productIdentifier);
}

public interface IInventoryManagementService
{
    event EventHandler? InventoryChanged;
    Task<Product> RecordStockMovementAsync(string productIdentifier, string movementType, int quantityChanged, string remarks = "");
    Task<IReadOnlyList<InventoryLog>> GetInventoryLogsAsync();

    // Raw Ingredients Management
    Task<IReadOnlyList<Ingredient>> GetIngredientsAsync();
    Task<Ingredient> SaveIngredientAsync(Ingredient ingredient);
    Task<Ingredient> AdjustIngredientStockAsync(string ingredientIdentifier, decimal quantityDelta, string movementType, string remarks);
    Task DeleteIngredientAsync(string ingredientIdentifier);
}

public interface IOrderTransactionService
{
    Task<Order> ProcessCheckoutAsync(CheckoutTransactionRequest request);
    Task<IReadOnlyList<Order>> GetOrdersAsync();
    Task<Order> UpdateOrderPreparationStatusAsync(string orderIdentifier, string preparationStatus);
    Task<Order> VoidOrderAsync(string orderIdentifier, string securityPin, string voidRemarks);
    Task<Order> VoidOrderAsync(string orderIdentifier, string securityPin);
}

public interface IAuditTrailService
{
    Task RecordAuditEntryAsync(string actionType, string targetResource, string actionDescription);
    Task<IReadOnlyList<AuditLog>> GetAuditLogsAsync();
}

public interface IImageStorageService
{
    Task<string> UploadProductImageAsync(Stream imageStream, string fileName);
    Task DeleteProductImageAsync(string imageUrl);
}

public interface IReportExportService
{
    Task<string> ExportSalesReportToExcelAsync(
        IReadOnlyList<Order> orders,
        IReadOnlyList<Product> products,
        string timeframeLabel);

    Task<string> ExportSalesReportToCsvAsync(
        IReadOnlyList<Order> orders,
        IReadOnlyList<Product> products,
        string timeframeLabel);
}

public interface IReceiptPrinterService
{
    bool IsSupported { get; }
    BluetoothPrinterDevice? SelectedPrinter { get; }
    int PaperWidthMillimeters { get; set; }
    int CharactersPerLine { get; }
    Task<IReadOnlyList<BluetoothPrinterDevice>> GetPairedPrintersAsync();
    void SelectPrinter(BluetoothPrinterDevice? printer);
    Task PrintOrderReceiptAsync(Order order);
    Task PrintTestReceiptAsync();
}

public interface IReceiptDocumentService
{
    Task<string?> GenerateReceiptPdfAsync(Order order);
}

public interface IDialogService
{
    Task ShowAlertAsync(string title, string message);
    Task<bool> ShowConfirmationAsync(string title, string message, string acceptText = "Yes", string cancelText = "Cancel");
    Task<string?> ShowPromptAsync(string title, string message, string placeholder = "", bool numeric = false, string initialValue = "");
    Task<string?> ShowActionSheetAsync(string title, string cancelText, params string[] actionButtons);
}
