using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Centralized session &amp; Role-Based Access Control (RBAC) state for the currently authenticated user.
/// Enforces the 3-tier access rules for Owner, Manager, and Staff.
/// </summary>
public class SessionService
{
    public UserAccount? CurrentUser { get; private set; }
    public UserAccount? ActiveUser => CurrentUser;

    public event EventHandler? SessionChanged;
    public event EventHandler? ActiveUserChanged;

    public bool IsAuthenticated => CurrentUser != null;
    public string Role => CurrentUser?.Role ?? string.Empty;
    public string FullName => CurrentUser?.FullName ?? string.Empty;

    public bool IsOwner => Role == UserRoles.Owner;
    public bool IsManager => Role == UserRoles.Manager;
    public bool IsStaff => Role == UserRoles.Staff;

    // ---- 3-Tier Role-Based Access Control Rules (Section 4A) ----
    public bool CanAccessDashboard => IsOwner || IsManager;
    public bool CanExportFinancialReports => IsOwner;
    public bool CanAccessPointOfSale => IsAuthenticated;
    public bool CanAccessProductRegistration => IsOwner || IsManager;
    public bool CanManageProducts => CanAccessProductRegistration;
    public bool CanViewCostAndProfitMargins => IsOwner;
    public bool CanAccessInventoryManagement => IsAuthenticated;
    public bool CanManageInventory => CanAccessInventoryManagement;
    public bool CanModifyInventoryStock => IsOwner || IsManager;
    public bool CanAccessUserManagement => IsOwner;
    public bool CanManageUserAccounts => CanAccessUserManagement;
    public bool CanAccessAuditTrail => IsOwner;
    public bool CanViewAuditTrail => CanAccessAuditTrail;
    public bool CanAuthorizeOrderVoid => IsOwner || IsManager;

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
