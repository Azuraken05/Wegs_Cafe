using System.Globalization;
using System.Text.RegularExpressions;

namespace Wegs_Cafe.Models;

public sealed class FormValidationSummary
{
    private readonly Dictionary<string, string> _fieldErrors = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> FieldErrors => _fieldErrors;
    public bool IsValid => _fieldErrors.Count == 0;

    public void AddError(string fieldKey, string message)
    {
        if (!_fieldErrors.ContainsKey(fieldKey))
            _fieldErrors[fieldKey] = message;
    }

    public string GetError(string fieldKey)
        => _fieldErrors.TryGetValue(fieldKey, out var error) ? error : string.Empty;

    public bool HasError(string fieldKey) => _fieldErrors.ContainsKey(fieldKey);

    public string FirstErrorOrEmpty => _fieldErrors.Values.FirstOrDefault() ?? string.Empty;
}

/// <summary>
/// Centralized real-time input validation rules for all 4 core forms:
/// User Registration, Product Registration, Inventory Adjustment, and Point of Sale Checkout.
/// </summary>
public static partial class ValidationRules
{
    [GeneratedRegex(@"^[A-Za-z\s]{2,60}$")]
    private static partial Regex FullNameRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^(?=.*[A-Z])(?=.*\d).{8,}$")]
    private static partial Regex PasswordStrengthRegex();

    [GeneratedRegex(@"^\d{4,6}$")]
    private static partial Regex PinRegex();

    [GeneratedRegex(@"^[A-Z0-9\-]{3,20}$")]
    private static partial Regex SkuRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]{6,20}$")]
    private static partial Regex GCashReferenceRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]{4,20}$")]
    private static partial Regex CardReferenceRegex();

    public static FormValidationSummary ValidateUserRegistration(
        string? fullName,
        string? emailAddress,
        string? password,
        string? securityPin,
        string? roleName,
        UserPermissions? permissions,
        IEnumerable<string>? existingEmails = null)
    {
        var summary = new FormValidationSummary();

        var trimmedName = fullName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName) || !FullNameRegex().IsMatch(trimmedName))
            summary.AddError("FullName", "Full Name must be 2 to 60 alphabetic characters and spaces only.");

        var trimmedEmail = emailAddress?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedEmail) || !EmailRegex().IsMatch(trimmedEmail))
        {
            summary.AddError("EmailAddress", "Enter a valid email address (e.g. barista@wegscafe.com).");
        }
        else if (existingEmails != null && existingEmails.Any(existing => string.Equals(existing, trimmedEmail, StringComparison.OrdinalIgnoreCase)))
        {
            summary.AddError("EmailAddress", "An account with this email address already exists.");
        }

        if (string.IsNullOrEmpty(password) || !PasswordStrengthRegex().IsMatch(password))
            summary.AddError("Password", "Password must be at least 8 characters with 1 uppercase letter and 1 number.");

        var trimmedPin = securityPin?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedPin) || !PinRegex().IsMatch(trimmedPin))
            summary.AddError("SecurityPin", "Quick-Switch PIN must be 4 to 6 numeric digits.");

        if (string.IsNullOrWhiteSpace(roleName))
            summary.AddError("RoleName", "Assigned User Level / Role is required.");

        if (permissions is null || !permissions.HasAnyPermission)
            summary.AddError("Permissions", "Enable at least one permission for the assigned role.");

        return summary;
    }

    public static FormValidationSummary ValidateProductRegistration(
        string? productName,
        string? stockKeepingUnit,
        string? category,
        string? sellingPriceText,
        string? costPriceText,
        string? initialStockText,
        string? lowStockThresholdText,
        IEnumerable<string>? existingSkus = null)
    {
        var summary = new FormValidationSummary();

        var trimmedName = productName?.Trim() ?? string.Empty;
        if (trimmedName.Length < 2 || trimmedName.Length > 80)
            summary.AddError("ProductName", "Product Name must be between 2 and 80 characters.");

        var normalizedSku = stockKeepingUnit?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!SkuRegex().IsMatch(normalizedSku))
        {
            summary.AddError("StockKeepingUnit", "SKU must be 3 to 20 uppercase letters, numbers, or hyphens.");
        }
        else if (existingSkus != null && existingSkus.Any(existing => string.Equals(existing, normalizedSku, StringComparison.OrdinalIgnoreCase)))
        {
            summary.AddError("StockKeepingUnit", "This Stock Keeping Unit (SKU) already exists.");
        }

        if (string.IsNullOrWhiteSpace(category))
            summary.AddError("Category", "Category selection is required.");

        var hasSellingPrice = decimal.TryParse(
            sellingPriceText?.Trim(),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var sellingPrice) && sellingPrice > 0m;
        if (!hasSellingPrice)
            summary.AddError("SellingPrice", "Selling Price must be greater than ₱0.00.");

        if (!string.IsNullOrWhiteSpace(costPriceText))
        {
            if (!decimal.TryParse(costPriceText.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var costPrice) || costPrice < 0m)
            {
                summary.AddError("CostPrice", "Cost Price must be ₱0.00 or greater.");
            }
            else if (hasSellingPrice && costPrice > sellingPrice)
            {
                summary.AddError("CostPrice", "Cost Price cannot exceed Selling Price.");
            }
        }

        if (!int.TryParse(initialStockText?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var initialStock) || initialStock < 0)
            summary.AddError("StockQuantity", "Initial Stock Quantity must be 0 or greater.");

        if (!int.TryParse(lowStockThresholdText?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var threshold) || threshold < 1)
            summary.AddError("LowStockThreshold", "Low-Stock Threshold must be 1 or greater.");

        return summary;
    }

    public static FormValidationSummary ValidateInventoryAdjustment(
        int currentStockQuantity,
        string? movementType,
        string? quantityChangeText,
        string? remarks)
    {
        var summary = new FormValidationSummary();

        if (string.IsNullOrWhiteSpace(movementType))
            summary.AddError("MovementType", "Movement Type is required.");

        if (!int.TryParse(quantityChangeText, out var quantityChange) || quantityChange == 0)
        {
            summary.AddError("QuantityChanged", "Quantity Change must be a non-zero whole number.");
        }
        else if (currentStockQuantity + quantityChange < 0)
        {
            summary.AddError("QuantityChanged", $"Final stock ({currentStockQuantity + quantityChange}) cannot be less than 0.");
        }

        var requiresNotes = string.Equals(movementType, InventoryMovementTypes.SpoilageOrWaste, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(movementType, InventoryMovementTypes.ManualCorrection, StringComparison.OrdinalIgnoreCase);

        if (requiresNotes && (remarks?.Trim().Length ?? 0) < 5)
            summary.AddError("Remarks", "Reason / Notes (at least 5 characters) are required for Spoilage or Manual Correction.");

        return summary;
    }

    public static FormValidationSummary ValidatePointOfSaleCheckout(
        int cartItemCount,
        string? customerOrTableLabel,
        string? paymentMethod,
        decimal totalAmount,
        string? amountTenderedText,
        string? paymentReferenceNumber)
    {
        var summary = new FormValidationSummary();

        if (cartItemCount <= 0)
            summary.AddError("OrderCart", "Order cart must contain at least 1 item.");

        if ((customerOrTableLabel?.Trim().Length ?? 0) < 2)
            summary.AddError("CustomerOrTableLabel", "Customer Name or Table Number (min 2 chars) is required.");

        if (string.Equals(paymentMethod, PaymentMethods.Cash, StringComparison.OrdinalIgnoreCase))
        {
            if (!decimal.TryParse(amountTenderedText, out var tendered) || tendered < totalAmount)
                summary.AddError("AmountTendered", $"Cash tendered must be at least ₱{totalAmount:N2}.");
        }
        else if (string.Equals(paymentMethod, PaymentMethods.GCash, StringComparison.OrdinalIgnoreCase))
        {
            var reference = paymentReferenceNumber?.Trim() ?? string.Empty;
            if (!GCashReferenceRegex().IsMatch(reference))
                summary.AddError("PaymentReferenceNumber", "GCash Reference Number must be 6 to 20 alphanumeric characters.");
        }
        else if (string.Equals(paymentMethod, PaymentMethods.CreditCard, StringComparison.OrdinalIgnoreCase))
        {
            var reference = paymentReferenceNumber?.Trim() ?? string.Empty;
            if (!CardReferenceRegex().IsMatch(reference))
                summary.AddError("PaymentReferenceNumber", "Approval / Trace Reference Number must be 4 to 20 alphanumeric characters.");
        }

        return summary;
    }
}
