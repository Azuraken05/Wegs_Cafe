using Firebase.Database.Query;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Manages Firebase Authentication + Realtime Database users/{user_identifier} sync,
/// quick-switch PIN login, and Owner staff registration using a secondary FirebaseAuthClient
/// so the active Owner session is preserved.
/// </summary>
public class UserAccountStore : IAuthenticationService, IUserManagementService
{
    private class StoredUserRecord
    {
        public UserAccount Account { get; set; } = new();
        public string PasswordHash { get; set; } = string.Empty;
    }

    private readonly SessionService _sessionService;
    private readonly IAuditTrailService _auditTrailService;
    private readonly FirebaseService _firebaseService;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "users_v3.json");
    private List<StoredUserRecord>? _cachedRecords;

    public UserAccountStore(
        SessionService sessionService,
        IAuditTrailService auditTrailService,
        FirebaseService firebaseService)
    {
        _sessionService = sessionService;
        _auditTrailService = auditTrailService;
        _firebaseService = firebaseService;
    }

    private async Task<List<StoredUserRecord>> EnsureLoadedAsync()
    {
        if (_cachedRecords != null) return _cachedRecords;

        _cachedRecords = await JsonStorageFile.ReadListAsync<StoredUserRecord>(_filePath);

        if (!_cachedRecords.Any(record => record.Account.IsOwner))
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

    public async Task<UserAccount> SignInAsync(string usernameOrEmail, string password)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrEmpty(password))
            throw new AuthenticationException("Please enter both your email/username and password.");

        var cleanInput = usernameOrEmail.Trim();

        // Attempt live Firebase Authentication when Web API key is configured and input is an email
        if (FirebaseConfiguration.IsWebApiKeyConfigured && cleanInput.Contains('@'))
        {
            try
            {
                var authResult = await _firebaseService.PrimaryAuthenticationClient
                    .SignInWithEmailAndPasswordAsync(cleanInput, password);

                if (authResult?.User != null)
                {
                    var remoteProfile = await _firebaseService.RealtimeDatabaseClient
                        .Child(DatabaseNodes.Users)
                        .Child(authResult.User.Uid)
                        .OnceSingleAsync<UserAccount>();

                    if (remoteProfile != null)
                    {
                        if (!remoteProfile.IsActive)
                            throw new AuthenticationException("This account has been deactivated. Please contact the Owner.");

                        _sessionService.StartSession(remoteProfile);
                        await _auditTrailService.RecordAuditEntryAsync(
                            AuditActionTypes.UserLogin,
                            $"users/{remoteProfile.UserIdentifier}",
                            $"{remoteProfile.FullName} ({remoteProfile.RoleDisplayName}) signed in via Firebase Auth.");
                        return remoteProfile;
                    }
                }
            }
            catch (AuthenticationException)
            {
                throw;
            }
            catch
            {
                // Fall back to local cache if offline
            }
        }

        UserAccount authenticatedAccount;
        await _mutex.WaitAsync();
        try
        {
            var records = await EnsureLoadedAsync();
            var matched = records.FirstOrDefault(record =>
                record.Account.Username.Equals(cleanInput, StringComparison.OrdinalIgnoreCase) ||
                record.Account.EmailAddress.Equals(cleanInput, StringComparison.OrdinalIgnoreCase));

            var isValid = matched != null && await Task.Run(() => PasswordHasher.Verify(password, matched.PasswordHash));
            if (!isValid)
                throw new AuthenticationException("Invalid email/username or password.");

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

    public async Task<UserAccount> SignInWithPinAsync(string securityPin)
    {
        if (string.IsNullOrWhiteSpace(securityPin))
            throw new AuthenticationException("Please enter your 4 to 6 digit Quick-Switch PIN.");

        UserAccount authenticatedAccount;
        await _mutex.WaitAsync();
        try
        {
            var records = await EnsureLoadedAsync();
            UserAccount? matchedAccount = null;

            foreach (var record in records.Where(r => r.Account.IsActive && !string.IsNullOrWhiteSpace(r.Account.SecurityPin)))
            {
                if (await Task.Run(() => PasswordHasher.Verify(securityPin.Trim(), record.Account.SecurityPin)))
                {
                    matchedAccount = record.Account;
                    break;
                }
            }

            if (matchedAccount == null)
                throw new AuthenticationException("Invalid Quick-Switch PIN. Please check your PIN and try again.");

            authenticatedAccount = matchedAccount;
            _sessionService.StartSession(authenticatedAccount);
        }
        finally
        {
            _mutex.Release();
        }

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.UserLogin,
            $"users/{authenticatedAccount.UserIdentifier}",
            $"{authenticatedAccount.FullName} ({authenticatedAccount.RoleDisplayName}) signed in via Quick-Switch PIN.");

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

        try
        {
            if (FirebaseConfiguration.IsWebApiKeyConfigured && _firebaseService.PrimaryAuthenticationClient.User != null)
                _firebaseService.PrimaryAuthenticationClient.SignOut();
        }
        catch
        {
            // Ignore remote sign-out errors
        }

        _sessionService.EndSession();
    }

    public async Task<UserAccount> AuthorizeSecurityPinAsync(string securityPin)
    {
        if (string.IsNullOrWhiteSpace(securityPin))
            throw new AuthenticationException("Please enter an authorized Security PIN.");

        await _mutex.WaitAsync();
        try
        {
            var records = await EnsureLoadedAsync();
            foreach (var record in records.Where(r =>
                         r.Account.IsActive &&
                         (r.Account.IsOwner || r.Account.Permissions.CanVoidOrders) &&
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
                .OrderBy(u => u.IsOwner ? 0 : u.IsManager ? 1 : 2)
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
        string emailAddress,
        string password,
        string securityPin,
        string roleName,
        UserPermissions permissions)
    {
        RequireOwnerAccess();

        UserAccount createdAccount;
        await _mutex.WaitAsync();
        try
        {
            var records = await EnsureLoadedAsync();
            var existingEmails = records.Select(r => r.Account.EmailAddress);

            var validation = ValidationRules.ValidateUserRegistration(
                fullName,
                emailAddress,
                password,
                securityPin,
                roleName,
                permissions,
                existingEmails);

            if (!validation.IsValid)
                throw new ValidationException(validation.FirstErrorOrEmpty);

            var cleanEmail = emailAddress.Trim();
            var derivedUsername = cleanEmail.Split('@')[0];
            var uid = Guid.NewGuid().ToString("N");

            // Attempt secondary Firebase Auth creation when Web API key is configured (Owner session remains intact)
            if (FirebaseConfiguration.IsWebApiKeyConfigured)
            {
                try
                {
                    var authCredential = await _firebaseService.SecondaryStaffRegistrationClient
                        .CreateUserWithEmailAndPasswordAsync(cleanEmail, password, fullName.Trim());
                    if (!string.IsNullOrWhiteSpace(authCredential?.User?.Uid))
                        uid = authCredential.User.Uid;
                }
                catch
                {
                    // Offline or Firebase error: continue with local persistence
                }
            }

            createdAccount = new UserAccount
            {
                UserIdentifier = uid,
                Username = derivedUsername,
                FullName = fullName.Trim(),
                EmailAddress = cleanEmail,
                RoleName = roleName.Trim(),
                Permissions = permissions,
                SecurityPin = await Task.Run(() => PasswordHasher.Hash(securityPin.Trim())),
                IsActive = true,
                CreatedAt = IsoTimestamp.Now(),
                CreatedBy = _sessionService.CurrentUser!.UserIdentifier
            };

            records.Add(new StoredUserRecord
            {
                Account = createdAccount,
                PasswordHash = await Task.Run(() => PasswordHasher.Hash(password))
            });

            await JsonStorageFile.WriteListAsync(_filePath, records);

            // Sync to Firebase Realtime Database (users/{user_identifier})
            try
            {
                await _firebaseService.RealtimeDatabaseClient
                    .Child(DatabaseNodes.Users)
                    .Child(createdAccount.UserIdentifier)
                    .PutAsync(createdAccount);
            }
            catch
            {
                // Continue seamlessly when offline
            }
        }
        finally
        {
            _mutex.Release();
        }

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.CreateStaff,
            $"users/{createdAccount.UserIdentifier}",
            $"Registered {createdAccount.RoleDisplayName} account '{createdAccount.EmailAddress}' ({createdAccount.FullName}).");

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

            if (target.Account.IsOwner)
                throw new ValidationException("The Owner account cannot be deactivated.");

            target.Account.IsActive = isActive;
            updatedAccount = target.Account;
            await JsonStorageFile.WriteListAsync(_filePath, records);

            try
            {
                await _firebaseService.RealtimeDatabaseClient
                    .Child(DatabaseNodes.Users)
                    .Child(userIdentifier)
                    .PutAsync(updatedAccount);
            }
            catch
            {
                // Continue seamlessly when offline
            }
        }
        finally
        {
            _mutex.Release();
        }

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.UpdateStaffStatus,
            $"users/{userIdentifier}",
            $"{(isActive ? "Activated" : "Deactivated")} account '{updatedAccount.EmailAddress}' ({updatedAccount.FullName}).");
    }

    private void RequireOwnerAccess()
    {
        if (!_sessionService.CanAccessUserManagement)
            throw new UnauthorizedAccessException("Only the Owner is permitted to manage staff accounts.");
    }
}
