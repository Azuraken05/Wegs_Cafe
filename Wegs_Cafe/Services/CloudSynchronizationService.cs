using Firebase.Database.Query;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

public enum CloudSyncStatus
{
    Synced,
    Syncing,
    Offline
}

/// <summary>
/// Dedicated background synchronization service using single-flight SemaphoreSlim(1, 1)
/// to process the SQLite Outbox queue, upload offline Cloudinary images, and synchronize with Firebase RTDB
/// with zero main thread contention on the MediaTek Helio G85.
/// </summary>
public class CloudSynchronizationService : IDisposable
{
    private readonly LocalDatabaseService _localDatabaseService;
    private readonly FirebaseService _firebaseService;
    private readonly IImageStorageService _imageStorageService;
    private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
    private IDispatcherTimer? _periodicSyncTimer;
    private bool _isDisposed;

    public CloudSyncStatus CurrentStatus { get; private set; } = CloudSyncStatus.Synced;
    public int PendingCount { get; private set; }
    public string StatusText { get; private set; } = "Synced";
    public string StatusIcon { get; private set; } = MaterialIcons.Assets.CloudDone;
    public Color StatusColor { get; private set; } = Color.FromArgb("#16A34A");

    public event Action<CloudSyncStatus, int, string, string, Color>? SyncStateChanged;

    public CloudSynchronizationService(
        LocalDatabaseService localDatabaseService,
        FirebaseService firebaseService,
        IImageStorageService imageStorageService)
    {
        _localDatabaseService = localDatabaseService;
        _firebaseService = firebaseService;
        _imageStorageService = imageStorageService;

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        InitializePeriodicSyncTimer();
        _ = SynchronizeAsync();
    }

    private void InitializePeriodicSyncTimer()
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            _periodicSyncTimer = dispatcher.CreateTimer();
            _periodicSyncTimer.Interval = TimeSpan.FromSeconds(30);
            _periodicSyncTimer.Tick += (_, _) => _ = SynchronizeAsync();
            _periodicSyncTimer.Start();
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        _ = SynchronizeAsync();
    }

    /// <summary>
    /// Single-flight synchronization worker. If another sync execution is actively running,
    /// subsequent requests are skipped immediately to prevent queue pileup on the Helio G85 CPU.
    /// </summary>
    public async Task SynchronizeAsync()
    {
        if (_isDisposed) return;

        // Non-blocking single-flight check
        if (!_syncSemaphore.Wait(0))
        {
            return;
        }

        try
        {
            var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            var pending = await _localDatabaseService.GetPendingSyncCountAsync().ConfigureAwait(false);

            if (!isOnline)
            {
                UpdateSyncState(
                    CloudSyncStatus.Offline,
                    pending,
                    pending > 0 ? $"Offline ({pending})" : "Offline",
                    MaterialIcons.Assets.CloudOff,
                    Color.FromArgb("#D97706"));
                return;
            }

            if (pending > 0)
            {
                UpdateSyncState(
                    CloudSyncStatus.Syncing,
                    pending,
                    $"Syncing ({pending})",
                    MaterialIcons.Assets.Sync,
                    Color.FromArgb("#0A0A0A"));
            }

            // Execute all network I/O, Cloudinary uploads, and SQLite outbox operations on background thread
            await Task.Run(async () =>
            {
                var operations = await _localDatabaseService.GetPendingOperationsFifoAsync().ConfigureAwait(false);
                foreach (var op in operations)
                {
                    try
                    {
                        if (string.Equals(op.OperationType, "CREATE_PRODUCT_WITH_IMAGE", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(op.LocalImageFilePath) &&
                            File.Exists(op.LocalImageFilePath))
                        {
                            await using var stream = File.OpenRead(op.LocalImageFilePath);
                            var cloudUrl = await _imageStorageService.UploadProductImageAsync(
                                stream,
                                Path.GetFileName(op.LocalImageFilePath)).ConfigureAwait(false);

                            var updatedPayload = op.JsonPayload.Replace("\"image_url\":\"\"", $"\"image_url\":\"{cloudUrl}\"");
                            await _firebaseService.RealtimeDatabaseClient
                                .Child(op.TargetFirebasePath)
                                .PutAsync(updatedPayload)
                                .ConfigureAwait(false);
                        }
                        else if (string.Equals(op.OperationType, "DELETE", StringComparison.OrdinalIgnoreCase))
                        {
                            await _firebaseService.RealtimeDatabaseClient
                                .Child(op.TargetFirebasePath)
                                .DeleteAsync()
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            await _firebaseService.RealtimeDatabaseClient
                                .Child(op.TargetFirebasePath)
                                .PutAsync(op.JsonPayload)
                                .ConfigureAwait(false);
                        }

                        await _localDatabaseService.RemovePendingOperationAsync(op.OperationIdentifier).ConfigureAwait(false);
                    }
                    catch
                    {
                        await _localDatabaseService.IncrementRetryCountAsync(op).ConfigureAwait(false);
                        if (op.RetryCount >= 5)
                        {
                            // Discard stalled operation after 5 failed retries to maintain queue health
                            await _localDatabaseService.RemovePendingOperationAsync(op.OperationIdentifier).ConfigureAwait(false);
                        }
                    }
                }
            }).ConfigureAwait(false);

            var remaining = await _localDatabaseService.GetPendingSyncCountAsync().ConfigureAwait(false);
            if (remaining == 0)
            {
                UpdateSyncState(
                    CloudSyncStatus.Synced,
                    0,
                    "Synced",
                    MaterialIcons.Assets.CloudDone,
                    Color.FromArgb("#16A34A"));
            }
            else
            {
                UpdateSyncState(
                    CloudSyncStatus.Syncing,
                    remaining,
                    $"Syncing ({remaining})",
                    MaterialIcons.Assets.Sync,
                    Color.FromArgb("#0A0A0A"));
            }
        }
        catch
        {
            // Transient sync error: state will be refreshed on next connectivity event or timer tick
        }
        finally
        {
            _syncSemaphore.Release();
        }
    }

    /// <summary>
    /// Throttled notification: only raises SyncStateChanged if the status or pending count actually changed,
    /// eliminating redundant UI layout invalidations and frame drops on the Helio G85.
    /// </summary>
    private void UpdateSyncState(
        CloudSyncStatus status,
        int pendingCount,
        string statusText,
        string statusIcon,
        Color statusColor)
    {
        if (CurrentStatus == status &&
            PendingCount == pendingCount &&
            StatusText == statusText &&
            StatusIcon == statusIcon &&
            StatusColor == statusColor)
        {
            return;
        }

        CurrentStatus = status;
        PendingCount = pendingCount;
        StatusText = statusText;
        StatusIcon = statusIcon;
        StatusColor = statusColor;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SyncStateChanged?.Invoke(CurrentStatus, PendingCount, StatusText, StatusIcon, StatusColor);
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _periodicSyncTimer?.Stop();
        _periodicSyncTimer = null;
        _syncSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
