using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Holds the signed-in user and answers every "is this role allowed?" question.
/// ViewModels and the Shell ask this class - never compare role strings elsewhere.
/// </summary>
public class SessionService
{
    public AppUser? CurrentUser { get; private set; }
    public event EventHandler? SessionChanged;

    public bool IsLoggedIn => CurrentUser != null;
    public string Role => CurrentUser?.Role ?? string.Empty;
    public string DisplayName => CurrentUser?.FullName ?? string.Empty;

    public bool IsOwner => Role == Roles.Owner;
    public bool IsManager => Role == Roles.Manager;
    public bool IsStaff => Role == Roles.Staff;

    // ---- Permission matrix (spec section 4A) ----
    public bool CanUsePos => IsLoggedIn;                                   // all roles
    public bool CanViewInventory => IsLoggedIn;                            // all roles (staff read-only)
    public bool CanEditInventory => IsOwner || IsManager;                  // restock / adjust
    public bool CanViewDashboard => IsOwner || IsManager;
    public bool CanManageProducts => IsOwner || IsManager;
    public bool CanViewProfit => IsOwner;                                  // cost price / margins
    public bool CanManageUsers => IsOwner;
    public bool CanViewAuditLogs => IsOwner;
    public bool CanVoidOrders => IsOwner;                                  // managers via PIN authorization
    public bool CanConfigurePrinter => IsOwner;

    public void SignIn(AppUser user)
    {
        CurrentUser = user;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut()
    {
        CurrentUser = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
