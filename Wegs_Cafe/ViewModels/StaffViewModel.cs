using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class StaffViewModel : BaseViewModel
{
    private readonly IUserService _users;
    private readonly SessionService _session;

    public ObservableCollection<AppUser> Users { get; } = new();
    public IReadOnlyList<string> AssignableRoles { get; } = new[] { Roles.Manager, Roles.Staff };

    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _pin = string.Empty;
    [ObservableProperty] private string _selectedRole = Roles.Staff;
    [ObservableProperty] private string _successMessage = string.Empty;

    public StaffViewModel(IUserService users, SessionService session)
    {
        _users = users;
        _session = session;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (!_session.CanManageUsers) return;
        try
        {
            var list = await _users.GetUsersAsync();
            Users.Clear();
            foreach (var u in list) Users.Add(u);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (IsBusy) return;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            IsBusy = true;
            var user = await _users.CreateStaffAsync(FullName, Username, Password, Pin, SelectedRole);
            SuccessMessage = $"Created {user.RoleDisplay} account \"{user.Username}\".";
            FullName = Username = Password = Pin = string.Empty;
            await LoadAsync();
        }
        catch (AuthException ex) { ErrorMessage = ex.Message; }
        catch (UnauthorizedAccessException ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(AppUser? user)
    {
        if (user == null) return;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            await _users.SetActiveAsync(user.Uid, !user.IsActive);
            await LoadAsync();
        }
        catch (Exception ex) when (ex is AuthException or UnauthorizedAccessException) { ErrorMessage = ex.Message; }
    }
}
