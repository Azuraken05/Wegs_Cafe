using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>Granular role permissions stored under users/{user_identifier}/permissions.</summary>
public class UserPermissions
{
    [JsonProperty("can_access_point_of_sale")]
    public bool CanAccessPointOfSale { get; set; } = true;

    [JsonProperty("can_view_inventory")]
    public bool CanViewInventory { get; set; } = true;

    [JsonProperty("can_restock_inventory")]
    public bool CanRestockInventory { get; set; }

    [JsonProperty("can_register_products")]
    public bool CanRegisterProducts { get; set; }

    [JsonProperty("can_void_orders")]
    public bool CanVoidOrders { get; set; }

    [JsonIgnore]
    public bool HasAnyPermission =>
        CanAccessPointOfSale ||
        CanViewInventory ||
        CanRestockInventory ||
        CanRegisterProducts ||
        CanVoidOrders;

    public static UserPermissions ForOwner() => new()
    {
        CanAccessPointOfSale = true,
        CanViewInventory = true,
        CanRestockInventory = true,
        CanRegisterProducts = true,
        CanVoidOrders = true
    };

    public static UserPermissions ForHeadBarista() => new()
    {
        CanAccessPointOfSale = true,
        CanViewInventory = true,
        CanRestockInventory = true,
        CanRegisterProducts = false,
        CanVoidOrders = true
    };

    public static UserPermissions ForBarista() => new()
    {
        CanAccessPointOfSale = true,
        CanViewInventory = true,
        CanRestockInventory = false,
        CanRegisterProducts = false,
        CanVoidOrders = false
    };
}

/// <summary>Firebase schema: users/{user_identifier}</summary>
public class UserAccount
{
    [JsonProperty("user_identifier")]
    public string UserIdentifier { get; set; } = string.Empty;

    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 60 characters.")]
    [RegularExpression(@"^[A-Za-z\s]+$", ErrorMessage = "Full name may contain letters and spaces only.")]
    [JsonProperty("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address (e.g. user@domain.com).")]
    [JsonProperty("email_address")]
    public string EmailAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Assigned user level or role is required.")]
    [JsonProperty("role_name")]
    public string RoleName { get; set; } = UserRoles.Barista;

    /// <summary>Compatibility alias mapped to RoleName.</summary>
    [JsonProperty("role")]
    public string Role
    {
        get => RoleName;
        set => RoleName = string.IsNullOrWhiteSpace(value) ? UserRoles.Barista : value;
    }

    [Required(ErrorMessage = "Quick-Switch PIN is required.")]
    [RegularExpression(@"^\d{4,6}$", ErrorMessage = "Quick-Switch PIN must be 4 to 6 numeric digits.")]
    [JsonProperty("security_pin")]
    public string SecurityPin { get; set; } = string.Empty;

    [JsonProperty("permissions")]
    public UserPermissions Permissions { get; set; } = UserPermissions.ForBarista();

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("created_at")]
    public string CreatedAt { get; set; } = IsoTimestamp.Now();

    [JsonProperty("created_by")]
    public string CreatedBy { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsOwner => string.Equals(RoleName, UserRoles.Owner, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsOwnerRole => IsOwner;

    [JsonIgnore]
    public bool IsManager =>
        string.Equals(RoleName, UserRoles.Manager, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(RoleName, UserRoles.HeadBarista, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsStaff => !IsOwner && !IsManager;

    [JsonIgnore]
    public bool CanBeDisabled => !IsOwner;

    [JsonIgnore]
    public string RoleDisplayName => string.IsNullOrEmpty(RoleName)
        ? string.Empty
        : char.ToUpperInvariant(RoleName[0]) + RoleName.Substring(1);

    [JsonIgnore]
    public string RoleBadgeText => (RoleName ?? string.Empty).ToUpperInvariant();

    [JsonIgnore]
    public string StatusBadgeText => IsActive ? "ACTIVE" : "INACTIVE";
}
