using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class InventoryLogsViewModel : BaseViewModel
{
    private readonly IInventoryManagementService _inventoryManagementService;
    private readonly SessionService _sessionService;
    private readonly IDialogService _dialogService;

    private List<InventoryLog> _allLogs = new();
    private CancellationTokenSource? _searchDebounceCts;
    private bool _hasLoadedInitialData;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedMovementFilter = "All Movements";

    [ObservableProperty]
    private int _totalLogCount;

    public IReadOnlyList<string> MovementFilterChips { get; } = InventoryMovementTypes.AllMovementFilters;

    public ObservableRangeCollection<InventoryLog> FilteredInventoryLogs { get; } = new();

    public InventoryLogsViewModel(
        IInventoryManagementService inventoryManagementService,
        SessionService sessionService,
        IDialogService dialogService)
    {
        _inventoryManagementService = inventoryManagementService;
        _sessionService = sessionService;
        _dialogService = dialogService;
        PageTitle = "Inventory Logs";
    }

    partial void OnSearchQueryChanged(string value)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (!token.IsCancellationRequested)
                    MainThread.BeginInvokeOnMainThread(ApplyFilters);
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    partial void OnSelectedMovementFilterChanged(string value) => ApplyFilters();

    [RelayCommand]
    public async Task LoadLogsAsync()
    {
        if (_hasLoadedInitialData || IsBusy) return;
        _hasLoadedInitialData = true;
        await RefreshLogsAsync();
    }

    [RelayCommand]
    public async Task RefreshLogsAsync()
    {
        if (IsBusy) return;
        ClearError();

        try
        {
            IsBusy = true;
            var logs = await _inventoryManagementService.GetInventoryLogsAsync();
            _allLogs = logs.ToList();
            TotalLogCount = _allLogs.Count;
            ApplyFilters();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectFilter(string filter)
    {
        SelectedMovementFilter = string.IsNullOrWhiteSpace(filter) ? "All Movements" : filter;
    }

    private void ApplyFilters()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filtered = _allLogs.AsEnumerable();

        if (!string.Equals(SelectedMovementFilter, "All Movements", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(log => string.Equals(log.MovementType, SelectedMovementFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(log =>
                (!string.IsNullOrEmpty(log.IngredientName) && log.IngredientName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(log.ProductName) && log.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(log.ReferenceReceiptNumber) && log.ReferenceReceiptNumber.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(log.PerformedByName) && log.PerformedByName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(log.Remarks) && log.Remarks.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        FilteredInventoryLogs.ReplaceAll(filtered.OrderByDescending(log => log.RecordedAt).ToList());
    }
}
