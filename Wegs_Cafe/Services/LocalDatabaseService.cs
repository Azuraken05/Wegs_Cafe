using SQLite;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// High-speed local SQLite cache &amp; FIFO Outbox queue for true offline mode and sub-200ms startup.
/// </summary>
public class LocalDatabaseService
{
    private const SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite |
        SQLiteOpenFlags.Create |
        SQLiteOpenFlags.SharedCache;

    private readonly SemaphoreSlim _initMutex = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    public string DatabasePath { get; } = Path.Combine(FileSystem.AppDataDirectory, "wegs_cafe_pos.db3");

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection != null) return _connection;

        await _initMutex.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection != null) return _connection;

            var connection = new SQLiteAsyncConnection(DatabasePath, Flags);
            await connection.CreateTableAsync<LocalUserEntity>().ConfigureAwait(false);
            await connection.CreateTableAsync<LocalProductEntity>().ConfigureAwait(false);
            await connection.CreateTableAsync<LocalOrderEntity>().ConfigureAwait(false);
            await connection.CreateTableAsync<PendingSyncOperationEntity>().ConfigureAwait(false);

            _connection = connection;
            return _connection;
        }
        finally
        {
            _initMutex.Release();
        }
    }

    public async Task<int> GetPendingSyncCountAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<PendingSyncOperationEntity>().CountAsync().ConfigureAwait(false);
    }

    public async Task EnqueueSyncOperationAsync(
        string operationType,
        string targetFirebasePath,
        string jsonPayload,
        string localImageFilePath = "")
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var entry = new PendingSyncOperationEntity
        {
            OperationIdentifier = Guid.NewGuid().ToString("N"),
            OperationType = operationType,
            TargetFirebasePath = targetFirebasePath,
            JsonPayload = jsonPayload,
            LocalImageFilePath = localImageFilePath,
            RetryCount = 0,
            CreatedAt = IsoTimestamp.Now()
        };
        await db.InsertAsync(entry).ConfigureAwait(false);
    }

    public async Task<List<PendingSyncOperationEntity>> GetPendingOperationsFifoAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<PendingSyncOperationEntity>()
            .OrderBy(op => op.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task RemovePendingOperationAsync(string operationIdentifier)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        await db.DeleteAsync<PendingSyncOperationEntity>(operationIdentifier).ConfigureAwait(false);
    }

    public async Task IncrementRetryCountAsync(PendingSyncOperationEntity operation)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        operation.RetryCount++;
        await db.UpdateAsync(operation).ConfigureAwait(false);
    }
}
