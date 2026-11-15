namespace Wegs_Cafe.Models;

/// <summary>
/// Firebase node names and the EXACT string values shared with the Laravel app.
/// Never rename these values - the web app reads the same strings.
/// </summary>
public static class DbNodes
{
    public const string Users = "users";
    public const string Products = "products";
    public const string Orders = "orders";
    public const string InventoryLogs = "inventory_logs";
    public const string AuditLogs = "audit_logs";
    public const string Shifts = "shifts";
}

public static class Roles
{
    public const string Owner = "owner";
    public const string Manager = "manager";
    public const string Staff = "staff";
    public static readonly string[] All = { Owner, Manager, Staff };
}

public static class ProductCategories
{
    public const string Coffee = "Coffee";
    public const string NonCoffee = "Non-Coffee";
    public const string Meals = "Meals";
    public const string Pastries = "Pastries";
    public const string AddOns = "Add-ons";
    public static readonly string[] All = { Coffee, NonCoffee, Meals, Pastries, AddOns };
}

public static class OrderTypes
{
    public const string DineIn = "Dine-In";
    public const string TakeOut = "Take-Out";
}

public static class PaymentMethods
{
    public const string Cash = "Cash";
    public const string GCash = "GCash";
    public const string CreditCard = "Credit Card";
}

public static class OrderStatuses
{
    public const string Completed = "Completed";
    public const string Voided = "Voided";
}

public static class InventoryChangeTypes
{
    public const string SaleDeduction = "Sale Deduction";
    public const string Restock = "Restock";
    public const string Spoilage = "Spoilage/Waste";
    public const string ManualAdjustment = "Manual Adjustment";
}

public static class AuditActions
{
    public const string CreateStaff = "CREATE_STAFF";
    public const string UpdatePrice = "UPDATE_PRICE";
    public const string CreateProduct = "CREATE_PRODUCT";
    public const string UpdateProduct = "UPDATE_PRODUCT";
    public const string VoidOrder = "VOID_ORDER";
    public const string PosCheckout = "POS_CHECKOUT";
    public const string RestockItem = "RESTOCK_ITEM";
    public const string AdjustStock = "ADJUST_STOCK";
    public const string DeleteProduct = "DELETE_PRODUCT";
    public const string ShiftClose = "SHIFT_CLOSE";
    public const string ShiftDiscrepancy = "SHIFT_DISCREPANCY";
}

