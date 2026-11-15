using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// On-device user store (JSON file) used until Firebase is integrated.
/// Passwords and PINs are stored only as PBKDF2 hashes.
/// </summary>
public class LocalUserStore : IAuthService, IUserService
{
    private class Record
    {
        public AppUser User { get; set; } = new();
        public string PasswordHash { get; set; } = string.Empty;
    }

    private static readonly Regex UsernameRule = new("^[A-Za-z0-9_.-]{3,20}$");
    private static readonly Regex PinRule = new("^[0-9]{4,6}$");

    private readonly SessionService _session;
    private readonly IAuditService _audit;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "local_users.json");
    private List<Record>? _records;

    public LocalUserStore(SessionService session, IAuditService audit)
    {
        _session = session;
        _audit = audit;
    }

    private async Task<List<Record>> LoadAsync()
    {
        if (_records != null) return _records;

        if (File.Exists(_path))
            _records = JsonConvert.DeserializeObject<List<Record>>(await File.ReadAllTextAsync(_path)) ?? new();
        else
            _records = new();

        if (!_records.Any(r => r.User.Role == Roles.Owner))
        {
            _records.Add(new Record
            {
                User = LocalSeed.CreateOwner(),
                PasswordHash = await Task.Run(() => PasswordHasher.Hash(LocalSeed.OwnerPassword))
            });
            await SaveAsync();
        }
        // Make sure the owner always has a PIN (void authorization)
        var owner = _records.First(r => r.User.Role == Roles.Owner);
        if (string.IsNullOrEmpty(owner.User.PinCode))
        {
            owner.User.PinCode = await Task.Run(() => PasswordHasher.Hash(LocalSeed.OwnerPin));
            await SaveAsync();
        }
        return _records;
    }

    private Task SaveAsync() => File.WriteAllTextAsync(_path, JsonConvert.SerializeObject(_records, Formatting.Indented));

    // ---------------- IAuthService ----------------
    public async Task<AppUser> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new AuthException("Enter your username and password.");

        await _lock.WaitAsync();
        try
        {
            var records = await LoadAsync();
            var record = records.FirstOrDefault(r =>
                r.User.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));

            var ok = record != null && await Task.Run(() => PasswordHasher.Verify(password, record.PasswordHash));
            if (!ok) throw new AuthException("Invalid username or password.");
            if (!record!.User.IsActive) throw new AuthException("This account has been disabled. Contact the owner.");

            _session.SignIn(record.User);
            return record.User;
        }
        finally { _lock.Release(); }
    }

    public void Logout() => _session.SignOut();

    public async Task<AppUser> AuthorizeManagerPinAsync(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin)) throw new AuthException("Enter the manager or owner PIN.");
        await _lock.WaitAsync();
        try
        {
            var records = await LoadAsync();
            foreach (var r in records.Where(r => r.User.IsActive && (r.User.Role == Roles.Owner || r.User.Role == Roles.Manager)
                                                 && !string.IsNullOrEmpty(r.User.PinCode)))
            {
                var user = r.User;
                if (await Task.Run(() => PasswordHasher.Verify(pin.Trim(), user.PinCode))) return user;
            }
            throw new AuthException("Incorrect PIN.");
        }
        finally { _lock.Release(); }
    }

    // ---------------- IUserService ----------------
    public async Task<IReadOnlyList<AppUser>> GetUsersAsync()
    {
        RequireOwner();
        await _lock.WaitAsync();
        try { return (await LoadAsync()).Select(r => r.User).OrderBy(u => u.Role == Roles.Owner ? 0 : 1).ThenBy(u => u.FullName).ToList(); }
        finally { _lock.Release(); }
    }

    public async Task<AppUser> CreateStaffAsync(string fullName, string username, string password, string pin, string role)
    {
        RequireOwner();

        fullName = fullName?.Trim() ?? "";
        username = username?.Trim() ?? "";
        if (fullName.Length < 2) throw new AuthException("Enter the full name.");
        if (!UsernameRule.IsMatch(username)) throw new AuthException("Username: 3-20 letters, numbers, . _ -");
        if ((password ?? "").Length < 6) throw new AuthException("Password must be at least 6 characters.");
        if (!PinRule.IsMatch(pin ?? "")) throw new AuthException("PIN must be 4 to 6 digits.");
        if (role != Roles.Manager && role != Roles.Staff) throw new AuthException("Role must be Manager or Staff.");

        await _lock.WaitAsync();
        try
        {
            var records = await LoadAsync();
            if (records.Any(r => r.User.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                throw new AuthException("That username is already taken.");

            var user = new AppUser
            {
                Uid = Guid.NewGuid().ToString("N"),
                Username = username,
                FullName = fullName,
                Email = $"{username.ToLowerInvariant()}@wegs.local",
                Role = role,
                PinCode = await Task.Run(() => PasswordHasher.Hash(pin!)),
                IsActive = true,
                CreatedBy = _session.CurrentUser!.Uid
            };
            records.Add(new Record { User = user, PasswordHash = await Task.Run(() => PasswordHasher.Hash(password!)) });
            await SaveAsync();

            await _audit.LogAsync(AuditActions.CreateStaff, $"users/{user.Uid}", $"Created {role} account \"{username}\" ({fullName}).");
            return user;
        }
        finally { _lock.Release(); }
    }

    public async Task SetActiveAsync(string uid, bool isActive)
    {
        RequireOwner();
        await _lock.WaitAsync();
        try
        {
            var record = (await LoadAsync()).FirstOrDefault(r => r.User.Uid == uid)
                         ?? throw new AuthException("User not found.");
            if (record.User.Role == Roles.Owner) throw new AuthException("The owner account cannot be disabled.");
            record.User.IsActive = isActive;
            await SaveAsync();
            await _audit.LogAsync("UPDATE_STAFF", $"users/{uid}", $"{(isActive ? "Enabled" : "Disabled")} account \"{record.User.Username}\".");
        }
        finally { _lock.Release(); }
    }

    private void RequireOwner()
    {
        if (!_session.CanManageUsers)
            throw new UnauthorizedAccessException("Only the owner can manage users.");
    }
}

