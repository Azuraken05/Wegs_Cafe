using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class AuditLogViewModel : BaseViewModel
{
    private readonly IAuditService _audit;
    private List<AuditLog> _all = new();

    public ObservableCollection<AuditLog> Items { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;

    public AuditLogViewModel(IAuditService audit) => _audit = audit;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            _all = (await _audit.GetLogsAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        Items.Clear();
        foreach (var l in _all.Where(l => q.Length == 0
                    || l.Action.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || l.ActorName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || l.Details.Contains(q, StringComparison.OrdinalIgnoreCase)))
            Items.Add(l);
    }
}
