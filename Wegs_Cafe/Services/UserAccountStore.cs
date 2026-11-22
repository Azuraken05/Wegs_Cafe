using System.Text.RegularExpressions;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Manages authentication and Owner staff registration while preserving the active Owner session.
/// Uses the shared snake_case UserAccount schema.
/// </summary>
public class UserAccountStore : IAuthenticationService, IUserManagementService
{
    private class StoredUserRecord
    {
        public UserAccount Account { get; set; } = new();
        public string PasswordHash { get; set; } = string.Empty;
    }

    private static readonly Regex UsernamePattern = new("^[A-Za-z0-9_.-]{3,24}$");
    private static readonly Regex SecurityPinPattern = new("^[0-9]{4,6}$");

    private readonly SessionService _sessionService;
    private readonly IAuditTrailService _auditTrailService;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "users_v2.json");
    private List<StoredUserRecord>? _cachedRecords;

    public UserAccountStore(SessionService sessionService, IAuditTrailService auditTrailService)
    {
        _sessionService = sessionService;
        _auditTrailService = auditTrailService;
    }

    private async Task<List<StoredUserRecord>> EnsureLoadedAsync()
    {
        if (_cachedRecords != null) return _cachedRecords;

        _cachedRecords = await JsonStorageFile.ReadListAsync<StoredUserRecord>(_filePath);

        if (!_cachedRecords.Any(record => record.Account.Role == UserRoles.Owner))
        {
            var defaultOwner = LocalOwnerSeed.CreateDefaultOwner();
            defaultOwner.SecurityPin = await Task.Run(() => PasswordHasher.Hash(LocalOwnerSeed.OwnerSecurityPin));
            _cachedRecords.Add(new StoredUserRecord
            {
                Account = defaultOwner,
                PasswordHash = await Task.Run(() => PasswordHasher.Hash(LocalOwnerSeed.OwnerPassword))
            });
            await JsonStorageFile.WriteListAsync(_filePath, _cachedRecords);
        }

        return _cachedRecords;
    }

    public async Task<UserAccount> SignInAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new AuthenticationException("Please enter both your username and password.");

        UserAccount authenticatedAccount;
        await _mutex.WaitAsync();
        try
        {
            var records = await EnsureLoadedAsync();
            var cleanInput = username.Trim();
            var matched = records.FirstOrDefault(record =>
                record.Account.Username.Equals(cleanInput, StringComparison.OrdinalIgnoreCase) ||
                record.Account.EmailAddress.Equals(cleanInput, StringComparison.OrdinalIgnoreCase));

            var isValid = matched != null && await Task.Run(() => PasswordHasher.Verify(password, matched.PasswordHash));
            if (!isValid)
                throw new AuthenticationException("Invalid username or password.");

            if (!matched!.Account.IsActive)
                throw new AuthenticationException("This account has been deactivated. Please contact the Owner.");

            authenticatedAccount = matched.Account;
            _sessionService.StartSession(authenticatedAccount);
        }
        finally
        {
            _mutex.Release();
        }

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.UserLogin,
            $"users/{authenticatedAccount.UserIdentifier}",
            $"{authenticatedAccount.FullName} ({authenticatedAccount.RoleDisplayName}) signed in.");

        return authenticatedAccount;
    }

    public async Task SignOutAsync()
    {
        if (_sessionService.CurrentUser != null)
        {
            await _auditTrailService.RecordAuditEntryAsync(
                AuditActionTypes.UserLogout,
                $"users/{_sessionService.CurrentUser.UserIdentifier}",
                $"{_sessionService.CurrentUser.FullName} signed out.");
        }
        _sessionService.EndSession();
    }

    public async Task<UserAccount> AuthorizeSecurityPinAsync(string securityPin)
    {
        if (string.IsNullOrWhiteSpace(securityPin))
            throw new AuthenticationException("Please enter a valid Owner or Manager security PIN.");

        await _mutex.WaitAsync();
        try
        {
            var records = await EnsureLoadedAsync();
            foreach (var record in records.Where(r =>
                         r.Account.IsActive &&
                         (r.Account.Role == UserRoles.Owner || r.Account.Role == UserRoles.Manager) &&
                         !string.IsNullOrWhiteSpace(r.Account.SecurityPin)))
            {
                if (await Task.Run(() => PasswordHasher.Verify(securityPin.Trim(), record.Account.SecurityPin)))
                    return record.Account;
            }
            throw new AuthenticationException("Security PIN authorization failed.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<UserAccount>> GetUserAccountsAsync()
    {
        RequireOwnerAccess();
        await _mutex.WaitAsync();
        try
        {
            var records = await EnsureLoadedAsync();
            return records
                .Select(r => r.Account)
                .OrderBy(u => u.Role == UserRoles.Owner ? 0 : u.Role == UserRoles.Manager ? 1 : 2)
                .ThenBy(u => u.FullName)
                .ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<UserAccount> RegisterStaffAccountAsync(
        string fullName,
        string username,
        string emailAddress,
        string password,
        string securityPin,
        string role)
    {
        RequireOwnerAccess();

        fullName = fullName?.Trim() ?? string.Empty;
        username = username?.Trim() ?? string.Empty;
        emailAddress = string.IsNullOrWhiteSpace(emailAddress)
            ? $"{username.ToLowerInvariant()}@wegscafe.local"
            : emailAddress.Trim();

        if (fullName.Length < 2)
            throw new ValidationException("Please enter the staff member's full name.");
        if (!UsernamePattern.IsMatch(username))
            throw new ValidationException("Username must be 3 to 24 letters, numbers, dots, underscores, or hyphens.");
        if ((password ?? string.Empty).Length < 6)
            throw new ValidationException("Password must be at least 6 characters long.");
        if (!SecurityPinPattern.IsMatch(securityPin ?? string.Empty))
            throw new ValidationException("Security PIN must be 4 to 6 numeric digits.");
        if (role != UserRoles.Manager && role != UserRoles.Staff)
            throw new ValidationException("Role must be either Manager or Staff.");

        UserAccount createdAccount;
        await _mutex.WaitAsync();
        try
        {
            var records = await EnsureLoadedAsync();
            if (records.Any(r => r.Account.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                throw new ValidationException("That username is already registered.");

            createdAccount = new UserAccount
            {
                UserIdentifier = Guid.NewGuid().ToString("N"),
                Username = username,
                FullName = fullName,
                EmailAddress = emailAddress,
                Role = role,
                SecurityPin = await Task.Run(() => PasswordHasher.Hash(securityPin!)),
                IsActive = true,
                CreatedAt = IsoTimestamp.Now(),
                CreatedBy = _sessionService.CurrentUser!.UserIdentifier
            };

            records.Add(new StoredUserRecord
            {
                Account = createdAccount,
                PasswordHash = await Task.Run(() => PasswordHasher.Hash(password!))
            });

            await JsonStorageFile.WriteListAsync(_filePath, records);
        }
        finally
        {
            _mutex.Release();
        }

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.CreateStaff,
            $"users/{createdAccount.UserIdentifier}",
            $"Registered {createdAccount.RoleDisplayName} account '{createdAccount.Username}' ({createdAccount.FullName}).");

        return createdAccount;
    }

    public async Task SetAccountActiveStatusAsync(string userIdentifier, bool isActive)
    {
        RequireOwnerAccess();
        UserAccount updatedAccount;
        await _mutex.WaitAsync();
        try
        {
            var records = await EnsureLoadedAsync();
            var target = records.FirstOrDefault(r => r.Account.UserIdentifier == userIdentifier)
                         ?? throw new ValidationException("User account was not found.");

            if (target.Account.Role == UserRoles.Owner)
                throw new ValidationException("The Owner account cannot be deactivated.");

            target.Account.IsActive = isActive;
            updatedAccount = target.Account;
            await JsonStorageFile.WriteListAsync(_filePath, records);
        }
        finally
        {
            _mutex.Release();
        }

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.UpdateStaffStatus,
            $"users/{userIdentifier}",
            $"{(isActive ? "Activated" : "Deactivated")} account '{updatedAccount.Username}' ({updatedAccount.FullName}).");
    }

    private void RequireOwnerAccess()
    {
        if (!_sessionService.CanAccessUserManagement)
            throw new UnauthorizedAccessException("Only the Owner is permitted to manage staff accounts.");
    }
}
