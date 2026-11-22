using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class UserManagementViewModel : BaseViewModel
{
    private readonly IUserManagementService _userManagementService;
    private readonly SessionService _sessionService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _emailAddress = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _securityPin = string.Empty;

    [ObservableProperty]
    private string _selectedRole = UserRoles.Staff;

    public IReadOnlyList<string> AssignableRoles { get; } = new[] { UserRoles.Staff, UserRoles.Manager };
    public ObservableCollection<UserAccount> UserAccounts { get; } = new();

    public UserManagementViewModel(
        IUserManagementService userManagementService,
        SessionService sessionService,
        IDialogService dialogService)
    {
        _userManagementService = userManagementService;
        _sessionService = sessionService;
        _dialogService = dialogService;
        PageTitle = "Staff & User Account Management";
    }

    [RelayCommand]
    public async Task LoadUserAccountsAsync()
    {
        if (IsBusy) return;
        ClearError();

        try
        {
            IsBusy = true;
            var accounts = await _userManagementService.GetUserAccountsAsync();
            UserAccounts.Clear();
            foreach (var account in accounts)
                UserAccounts.Add(account);
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
    private async Task RegisterStaffAccountAsync()
    {
        if (IsBusy) return;
        ClearError();

        if (!_sessionService.IsOwner)
        {
            ErrorMessage = "Only the Owner account is authorized to register staff or manager accounts.";
            return;
        }

        try
        {
            IsBusy = true;
            var created = await _userManagementService.RegisterStaffAccountAsync(
                FullName,
                Username,
                EmailAddress,
                Password,
                SecurityPin,
                SelectedRole);

            FullName = string.Empty;
            Username = string.Empty;
            EmailAddress = string.Empty;
            Password = string.Empty;
            SecurityPin = string.Empty;
            SelectedRole = UserRoles.Staff;

            var accounts = await _userManagementService.GetUserAccountsAsync();
            UserAccounts.Clear();
            foreach (var account in accounts)
                UserAccounts.Add(account);

            await _dialogService.ShowAlertAsync(
                "Staff Account Created",
                $"Registered {created.FullName} (@{created.Username}) with role '{created.Role.ToUpperInvariant()}'.");
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
    private async Task ToggleAccountActiveStatusAsync(UserAccount? account)
    {
        if (account is null) return;

        if (account.IsOwnerRole)
        {
            await _dialogService.ShowAlertAsync("Protected Account", "The primary Owner account cannot be deactivated.");
            return;
        }

        var nextState = !account.IsActive;
        var actionWord = nextState ? "Activate" : "Deactivate";

        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"{actionWord} Account",
            $"{actionWord} staff account for {account.FullName} (@{account.Username})?",
            actionWord,
            "Cancel");

        if (!confirmed) return;

        try
        {
            await _userManagementService.SetAccountActiveStatusAsync(account.UserIdentifier, nextState);
            await LoadUserAccountsAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Status Update Error", ex.Message);
        }
    }
}
