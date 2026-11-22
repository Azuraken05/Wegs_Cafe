using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Immutable local audit log repository storing audit_logs entries in snake_case JSON.
/// </summary>
public class AuditTrailService : IAuditTrailService
{
    private readonly SessionService _sessionService;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "audit_logs_v2.json");

    public AuditTrailService(SessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public async Task RecordAuditEntryAsync(string actionType, string targetResource, string actionDescription)
    {
        await _mutex.WaitAsync();
        try
        {
            var logs = await JsonStorageFile.ReadListAsync<AuditLog>(_filePath);
            logs.Add(new AuditLog
            {
                AuditLogIdentifier = Guid.NewGuid().ToString("N"),
                ActorIdentifier = _sessionService.CurrentUser?.UserIdentifier ?? "system",
                ActorName = _sessionService.CurrentUser?.FullName ?? "System",
                ActorRole = _sessionService.Role,
                ActionType = actionType,
                TargetResource = targetResource,
                ActionDescription = actionDescription,
                RecordedAt = IsoTimestamp.Now()
            });
            await JsonStorageFile.WriteListAsync(_filePath, logs);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<AuditLog>> GetAuditLogsAsync()
    {
        if (!_sessionService.CanAccessAuditTrail)
            throw new UnauthorizedAccessException("Only the Owner can view the Audit Trail.");

        await _mutex.WaitAsync();
        try
        {
            var logs = await JsonStorageFile.ReadListAsync<AuditLog>(_filePath);
            return logs.OrderByDescending(entry => entry.RecordedAt).ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }
}
