using SQLite;

namespace Wegs_Cafe.Models;

[Table("local_users")]
public class LocalUserEntity
{
    [PrimaryKey]
    public string UserIdentifier { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string RoleName { get; set; } = UserRoles.Barista;
    public string PasswordHash { get; set; } = string.Empty;
    public string SecurityPinHash { get; set; } = string.Empty;
    public string PermissionsJson { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string CreatedAt { get; set; } = IsoTimestamp.Now();
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsSynced { get; set; }
}

[Table("local_products")]
public class LocalProductEntity
{
    [PrimaryKey]
    public string ProductIdentifier { get; set; } = string.Empty;
    [Indexed]
    public string StockKeepingUnit { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    [Indexed]
    public string Category { get; set; } = ProductCategories.HotCoffee;
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal CalculatedRecipeCost { get; set; }
    public string RecipeIngredientsJson { get; set; } = "[]";
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; } = 10;
    public string MeasurementUnit { get; set; } = "cups";
    public bool AllowDrinkModifiers { get; set; } = true;
    public string ImageUrl { get; set; } = string.Empty;
    public string LocalCachedImagePath { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public string UpdatedAt { get; set; } = IsoTimestamp.Now();
    public bool IsSynced { get; set; }
}

[Table("local_ingredients")]
public class LocalIngredientEntity
{
    [PrimaryKey]
    public string IngredientIdentifier { get; set; } = string.Empty;
    [Indexed]
    public string StockKeepingUnit { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    [Indexed]
    public string Category { get; set; } = IngredientCategories.DairyAndMilk;
    public string MeasurementUnit { get; set; } = MeasurementUnits.Milliliters;
    public decimal CurrentStockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; } = 100m;
    public decimal UnitCost { get; set; }
    public string UpdatedAt { get; set; } = IsoTimestamp.Now();
    public bool IsSynced { get; set; }
}

[Table("local_inventory_logs")]
public class LocalInventoryLogEntity
{
    [PrimaryKey]
    public string InventoryLogIdentifier { get; set; } = string.Empty;
    [Indexed]
    public string IngredientIdentifier { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public string ProductIdentifier { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string MovementType { get; set; } = InventoryMovementTypes.ManualCorrection;
    public decimal QuantityChanged { get; set; }
    public decimal PreviousStockQuantity { get; set; }
    public decimal NewStockQuantity { get; set; }
    public string MeasurementUnit { get; set; } = string.Empty;
    public string ReferenceReceiptNumber { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string PerformedByIdentifier { get; set; } = string.Empty;
    public string PerformedByName { get; set; } = string.Empty;
    [Indexed]
    public string RecordedAt { get; set; } = IsoTimestamp.Now();
    public bool IsSynced { get; set; }
}

[Table("local_orders")]
public class LocalOrderEntity
{
    [PrimaryKey]
    public string OrderIdentifier { get; set; } = string.Empty;
    public string ReceiptNumber { get; set; } = string.Empty;
    public string OrderType { get; set; } = OrderTypes.DineIn;
    public string CustomerOrTableLabel { get; set; } = string.Empty;
    public string PreparationStatus { get; set; } = OrderPreparationStatuses.Preparing;
    public string OrderItemsJson { get; set; } = "[]";
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;
    public string PaymentReferenceNumber { get; set; } = string.Empty;
    public decimal AmountTendered { get; set; }
    public decimal ChangeDue { get; set; }
    public string OrderStatus { get; set; } = OrderStatuses.Completed;
    public string BaristaIdentifier { get; set; } = string.Empty;
    public string BaristaName { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = IsoTimestamp.Now();
    public string VoidedAt { get; set; } = string.Empty;
    public string VoidedByIdentifier { get; set; } = string.Empty;
    public string VoidedByName { get; set; } = string.Empty;
    public string VoidRemarks { get; set; } = string.Empty;
    public bool IsParked { get; set; }
    public bool IsSynced { get; set; }
}

[Table("pending_sync_operations")]
public class PendingSyncOperationEntity
{
    [PrimaryKey]
    public string OperationIdentifier { get; set; } = Guid.NewGuid().ToString("N");
    public string OperationType { get; set; } = string.Empty;
    public string TargetFirebasePath { get; set; } = string.Empty;
    public string JsonPayload { get; set; } = string.Empty;
    public string LocalImageFilePath { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    [Indexed]
    public string CreatedAt { get; set; } = IsoTimestamp.Now();
}
