using Newtonsoft.Json;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>Append-only on-device audit log (JSON file) until Firebase is integrated.</summary>
public class LocalAuditService : IAuditService
{
    private readonly SessionService _session;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "local_audit_logs.json");

    public LocalAuditService(SessionService session) => _session = session;

    public async Task LogAsync(string action, string targetEntity, string details)
    {
        await _lock.WaitAsync();
        try
        {
            var logs = await ReadAsync();
            logs.Add(new AuditLog
            {
                AuditId = Guid.NewGuid().ToString("N"),
                ActorUid = _session.CurrentUser?.Uid ?? "system",
                ActorName = _session.CurrentUser?.FullName ?? "system",
                ActorRole = _session.Role,
                Action = action,
                TargetEntity = targetEntity,
                Details = details
            });
            await File.WriteAllTextAsync(_path, JsonConvert.SerializeObject(logs, Formatting.Indented));
        }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<AuditLog>> GetLogsAsync()
    {
        if (!_session.CanViewAuditLogs) throw new UnauthorizedAccessException("Only the owner can view audit logs.");
        await _lock.WaitAsync();
        try { return (await ReadAsync()).OrderByDescending(l => l.Timestamp).ToList(); }
        finally { _lock.Release(); }
    }

    private async Task<List<AuditLog>> ReadAsync()
        => File.Exists(_path)
            ? JsonConvert.DeserializeObject<List<AuditLog>>(await File.ReadAllTextAsync(_path)) ?? new()
            : new();
}
