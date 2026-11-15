using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

public class AuthException : Exception
{
    public AuthException(string message) : base(message) { }
}

/// <summary>Sign-in contract. Today: LocalUserStore. Later: a Firebase implementation.</summary>
public interface IAuthService
{
    /// <summary>Validates credentials, starts the session, returns the user. Throws AuthException.</summary>
    Task<AppUser> LoginAsync(string username, string password);
    void Logout();
    /// <summary>Checks a PIN against active owner/manager accounts. Returns the authorizer or throws AuthException.</summary>
    Task<AppUser> AuthorizeManagerPinAsync(string pin);
}

/// <summary>User administration contract (owner only).</summary>
public interface IUserService
{
    Task<IReadOnlyList<AppUser>> GetUsersAsync();
    Task<AppUser> CreateStaffAsync(string fullName, string username, string password, string pin, string role);
    Task SetActiveAsync(string uid, bool isActive);
}

public interface IAuditService
{
    Task LogAsync(string action, string targetEntity, string details);
    Task<IReadOnlyList<AuditLog>> GetLogsAsync();
}

