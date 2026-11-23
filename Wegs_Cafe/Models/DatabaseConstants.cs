namespace Wegs_Cafe.Models;

/// <summary>
/// Firebase Realtime Database node names and exact string constants shared with the Laravel web application.
/// Full descriptive names are used everywhere (zero abbreviations).
/// </summary>
public static class DatabaseNodes
{
    public const string Users = "users";
    public const string Products = "products";
    public const string Ingredients = "ingredients";
    public const string Orders = "orders";
    public const string InventoryLogs = "inventory_logs";
    public const string AuditLogs = "audit_logs";
    public const string Shifts = "shifts";
}

public static class UserRoles
{
    public const string Owner = "owner";
    public const string Manager = "manager";
    public const string HeadBarista = "Head Barista";
    public const string Barista = "Barista";
    public const string Staff = "staff";
    public static readonly string[] All = { Owner, HeadBarista, Barista, Manager, Staff };
    public static readonly string[] StaffAssignable = { Barista, HeadBarista };
}

public static class ProductCategories
{
    public const string HotCoffee = "Hot Coffee";
    public const string IcedCoffee = "Iced Coffee";
    public const string Coffee = "Coffee";
    public const string NonCoffee = "Non-Coffee";
    public const string Meals = "Meals";
    public const string Pastries = "Pastries";
    public const string AddOns = "Add-ons";

    public static readonly string[] All =
    {
        HotCoffee, IcedCoffee, NonCoffee, Meals, Pastries, AddOns
    };

    public static readonly string[] AllWithFilter =
    {
        "All", HotCoffee, IcedCoffee, NonCoffee, Meals, Pastries, AddOns
    };
}

public static class IngredientCategories
{
    public const string DairyAndMilk = "Dairy & Milk";
    public const string CoffeeAndTea = "Coffee & Tea";
    public const string SyrupsAndSauces = "Syrups & Sauces";
    public const string DryGoodsAndSpices = "Dry Goods & Spices";
    public const string PackagingAndCups = "Packaging & Cups";

    public static readonly string[] All =
    {
        DairyAndMilk, CoffeeAndTea, SyrupsAndSauces, DryGoodsAndSpices, PackagingAndCups
    };

    public static readonly string[] AllWithFilter =
    {
        "All", DairyAndMilk, CoffeeAndTea, SyrupsAndSauces, DryGoodsAndSpices, PackagingAndCups
    };
}

public static class MeasurementUnits
{
    public const string Milliliters = "ml";
    public const string Grams = "g";
    public const string Pieces = "pcs";

    public static readonly string[] All = { Milliliters, Grams, Pieces };
}

public static class OrderTypes
{
    public const string DineIn = "Dine-In";
    public const string TakeOut = "Take-Out";
}

public static class OrderPreparationStatuses
{
    public const string Preparing = "Preparing";
    public const string Ready = "Ready";
    public const string Completed = "Completed";
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

public static class InventoryMovementTypes
{
    public const string SaleRecipeDeduction = "Sale Recipe Deduction";
    public const string StockReplenishment = "Stock Replenishment";
    public const string SpoilageOrWaste = "Spoilage or Waste";
    public const string VoidRestoration = "Void Restoration";
    public const string BaristaSpillOrRemake = "Barista Spill or Remake";
    public const string ManualCorrection = "Manual Correction";
    public const string ManualAdjustment = ManualCorrection;
    public const string SaleDeduction = SaleRecipeDeduction;

    public static readonly string[] EditableMovements =
    {
        StockReplenishment,
        SpoilageOrWaste,
        BaristaSpillOrRemake,
        ManualCorrection
    };

    public static readonly string[] AllMovementFilters =
    {
        "All Movements",
        SaleRecipeDeduction,
        StockReplenishment,
        SpoilageOrWaste,
        VoidRestoration,
        ManualCorrection
    };
}

public static class AuditActionTypes
{
    public const string UserLogin = "USER_LOGIN";
    public const string UserLogout = "USER_LOGOUT";
    public const string CreateStaff = "CREATE_STAFF";
    public const string RegisterStaff = CreateStaff;
    public const string UpdateStaffStatus = "UPDATE_STAFF_STATUS";
    public const string UpdateStaffPermissions = "UPDATE_STAFF_PERMISSIONS";
    public const string CreateProduct = "CREATE_PRODUCT";
    public const string UpdateProduct = "UPDATE_PRODUCT";
    public const string UpdatePrice = "UPDATE_PRICE";
    public const string ToggleProductAvailability = "TOGGLE_PRODUCT_AVAILABILITY";
    public const string DeleteProduct = "DELETE_PRODUCT";
    public const string PointOfSaleCheckout = "POINT_OF_SALE_CHECKOUT";
    public const string ProcessOrder = PointOfSaleCheckout;
    public const string UpdateOrderPreparationStatus = "UPDATE_ORDER_PREPARATION_STATUS";
    public const string VoidOrder = "VOID_ORDER";
    public const string StockReplenishment = "STOCK_REPLENISHMENT";
    public const string ManualStockAdjustment = "MANUAL_STOCK_ADJUSTMENT";
    public const string AdjustStock = ManualStockAdjustment;
    public const string ExportSalesReport = "EXPORT_SALES_REPORT";
    public const string ShiftOpen = "SHIFT_OPEN";
    public const string ShiftClose = "SHIFT_CLOSE";
    public const string ShiftDiscrepancy = "SHIFT_DISCREPANCY";
}
