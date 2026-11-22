using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class AuditTrailViewModel : BaseViewModel
{
    private readonly IAuditTrailService _auditTrailService;
    private List<AuditLog> _allAuditLogs = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedActionFilter = "All";

    public IReadOnlyList<string> ActionFilterOptions { get; } = new[]
    {
        "All",
        AuditActionTypes.UserLogin,
        AuditActionTypes.CreateProduct,
        AuditActionTypes.UpdateProduct,
        AuditActionTypes.DeleteProduct,
        AuditActionTypes.AdjustStock,
        AuditActionTypes.ProcessOrder,
        AuditActionTypes.VoidOrder,
        AuditActionTypes.RegisterStaff,
        AuditActionTypes.ExportSalesReport
    };

    public ObservableCollection<AuditLog> FilteredAuditLogs { get; } = new();

    public AuditTrailViewModel(IAuditTrailService auditTrailService)
    {
        _auditTrailService = auditTrailService;
        PageTitle = "Immutable System Audit Trail";
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedActionFilterChanged(string value) => ApplyFilters();

    [RelayCommand]
    public async Task LoadAuditLogsAsync()
    {
        if (IsBusy) return;
        ClearError();

        try
        {
            IsBusy = true;
            var logs = await _auditTrailService.GetAuditLogsAsync();
            _allAuditLogs = logs.ToList();
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
    private void SelectActionFilter(string actionType)
    {
        SelectedActionFilter = string.IsNullOrWhiteSpace(actionType) ? "All" : actionType;
    }

    private void ApplyFilters()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filtered = _allAuditLogs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedActionFilter) &&
            !string.Equals(SelectedActionFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(entry => string.Equals(entry.ActionType, SelectedActionFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(entry =>
                entry.ActorName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                entry.ActionType.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                entry.ActionDescription.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                entry.TargetResource.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredAuditLogs.Clear();
        foreach (var entry in filtered)
            FilteredAuditLogs.Add(entry);
    }
}
