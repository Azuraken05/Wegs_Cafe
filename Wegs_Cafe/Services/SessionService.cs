using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Centralized session &amp; granular Role-Based Access Control (RBAC) state for the currently authenticated user.
/// </summary>
public class SessionService
{
    public UserAccount? CurrentUser { get; private set; }
    public UserAccount? ActiveUser => CurrentUser;

    public event EventHandler? SessionChanged;
    public event EventHandler? ActiveUserChanged;

    public bool IsAuthenticated => CurrentUser != null;
    public string Role => CurrentUser?.RoleName ?? string.Empty;
    public string FullName => CurrentUser?.FullName ?? string.Empty;

    public bool IsOwner => CurrentUser?.IsOwner ?? false;
    public bool IsManager => CurrentUser?.IsManager ?? false;
    public bool IsStaff => CurrentUser?.IsStaff ?? false;

    // ---- Granular Role Permissions (Section 3A) ----
    public bool CanAccessPointOfSale => IsAuthenticated && (IsOwner || (CurrentUser?.Permissions.CanAccessPointOfSale ?? false));
    public bool CanViewInventory => IsAuthenticated && (IsOwner || (CurrentUser?.Permissions.CanViewInventory ?? false));
    public bool CanRestockInventory => IsAuthenticated && (IsOwner || (CurrentUser?.Permissions.CanRestockInventory ?? false));
    public bool CanRegisterProducts => IsAuthenticated && (IsOwner || (CurrentUser?.Permissions.CanRegisterProducts ?? false));
    public bool CanVoidOrders => IsAuthenticated && (IsOwner || (CurrentUser?.Permissions.CanVoidOrders ?? false));

    // ---- Navigation & Compatibility Aliases ----
    public bool CanAccessUserManagement => IsOwner;
    public bool CanManageUserAccounts => CanAccessUserManagement;
    public bool CanAccessProductRegistration => CanRegisterProducts;
    public bool CanManageProducts => CanRegisterProducts;
    public bool CanAccessInventoryManagement => CanViewInventory;
    public bool CanManageInventory => CanViewInventory;
    public bool CanModifyInventoryStock => CanRestockInventory;
    public bool CanAuthorizeOrderVoid => CanVoidOrders;
    public bool CanAccessDashboard => IsOwner || IsManager;
    public bool CanExportFinancialReports => IsOwner;
    public bool CanViewCostAndProfitMargins => IsOwner;
    public bool CanAccessAuditTrail => IsOwner;
    public bool CanViewAuditTrail => IsOwner;

    public void StartSession(UserAccount userAccount)
    {
        CurrentUser = userAccount;
        SessionChanged?.Invoke(this, EventArgs.Empty);
        ActiveUserChanged?.Invoke(this, EventArgs.Empty);
    }

    public void EndSession()
    {
        CurrentUser = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
        ActiveUserChanged?.Invoke(this, EventArgs.Empty);
    }
}
