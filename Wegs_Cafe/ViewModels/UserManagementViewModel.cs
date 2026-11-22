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
    private string _emailAddress = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _securityPin = string.Empty;

    [ObservableProperty]
    private string _selectedRolePreset = UserRoles.Barista;

    [ObservableProperty]
    private string _customRoleName = UserRoles.Barista;

    [ObservableProperty]
    private bool _canAccessPointOfSale = true;

    [ObservableProperty]
    private bool _canViewInventory = true;

    [ObservableProperty]
    private bool _canRestockInventory;

    [ObservableProperty]
    private bool _canRegisterProducts;

    [ObservableProperty]
    private bool _canVoidOrders;

    // Inline field-level validation error properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFullNameError))]
    private string _fullNameError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmailAddressError))]
    private string _emailAddressError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPasswordError))]
    private string _passwordError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSecurityPinError))]
    private string _securityPinError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoleNameError))]
    private string _roleNameError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPermissionsError))]
    private string _permissionsError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitForm))]
    private bool _isFormValid;

    public bool HasFullNameError => !string.IsNullOrWhiteSpace(FullNameError);
    public bool HasEmailAddressError => !string.IsNullOrWhiteSpace(EmailAddressError);
    public bool HasPasswordError => !string.IsNullOrWhiteSpace(PasswordError);
    public bool HasSecurityPinError => !string.IsNullOrWhiteSpace(SecurityPinError);
    public bool HasRoleNameError => !string.IsNullOrWhiteSpace(RoleNameError);
    public bool HasPermissionsError => !string.IsNullOrWhiteSpace(PermissionsError);
    public bool IsCustomRolePreset => string.Equals(SelectedRolePreset, "Custom Role", StringComparison.OrdinalIgnoreCase);
    public bool CanSubmitForm => IsFormValid && IsNotBusy;

    public IReadOnlyList<string> RolePresetOptions { get; } = new[]
    {
        UserRoles.Barista,
        UserRoles.HeadBarista,
        "Custom Role"
    };

    public ObservableCollection<UserAccount> UserAccounts { get; } = new();

    public UserManagementViewModel(
        IUserManagementService userManagementService,
        SessionService sessionService,
        IDialogService dialogService)
    {
        _userManagementService = userManagementService;
        _sessionService = sessionService;
        _dialogService = dialogService;
        PageTitle = "User & Role Management";

        ValidateFormLive();
    }

    partial void OnFullNameChanged(string value) => ValidateFormLive();
    partial void OnEmailAddressChanged(string value) => ValidateFormLive();
    partial void OnPasswordChanged(string value) => ValidateFormLive();
    partial void OnSecurityPinChanged(string value) => ValidateFormLive();
    partial void OnCustomRoleNameChanged(string value) => ValidateFormLive();
    partial void OnCanAccessPointOfSaleChanged(bool value) => ValidateFormLive();
    partial void OnCanViewInventoryChanged(bool value) => ValidateFormLive();
    partial void OnCanRestockInventoryChanged(bool value) => ValidateFormLive();
    partial void OnCanRegisterProductsChanged(bool value) => ValidateFormLive();
    partial void OnCanVoidOrdersChanged(bool value) => ValidateFormLive();

    partial void OnSelectedRolePresetChanged(string value)
    {
        OnPropertyChanged(nameof(IsCustomRolePreset));

        if (string.Equals(value, UserRoles.Barista, StringComparison.OrdinalIgnoreCase))
        {
            CustomRoleName = UserRoles.Barista;
            CanAccessPointOfSale = true;
            CanViewInventory = true;
            CanRestockInventory = false;
            CanRegisterProducts = false;
            CanVoidOrders = false;
        }
        else if (string.Equals(value, UserRoles.HeadBarista, StringComparison.OrdinalIgnoreCase))
        {
            CustomRoleName = UserRoles.HeadBarista;
            CanAccessPointOfSale = true;
            CanViewInventory = true;
            CanRestockInventory = true;
            CanRegisterProducts = false;
            CanVoidOrders = true;
        }
        else
        {
            CustomRoleName = string.Empty;
        }

        ValidateFormLive();
    }

    private UserPermissions BuildCurrentPermissions() => new()
    {
        CanAccessPointOfSale = CanAccessPointOfSale,
        CanViewInventory = CanViewInventory,
        CanRestockInventory = CanRestockInventory,
        CanRegisterProducts = CanRegisterProducts,
        CanVoidOrders = CanVoidOrders
    };

    private void ValidateFormLive()
    {
        var effectiveRole = IsCustomRolePreset ? CustomRoleName : SelectedRolePreset;
        var existingEmails = UserAccounts.Select(u => u.EmailAddress);

        var summary = ValidationRules.ValidateUserRegistration(
            FullName,
            EmailAddress,
            Password,
            SecurityPin,
            effectiveRole,
            BuildCurrentPermissions(),
            existingEmails);

        FullNameError = summary.GetError("FullName");
        EmailAddressError = summary.GetError("EmailAddress");
        PasswordError = summary.GetError("Password");
        SecurityPinError = summary.GetError("SecurityPin");
        RoleNameError = summary.GetError("RoleName");
        PermissionsError = summary.GetError("Permissions");
        IsFormValid = summary.IsValid;
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

            ValidateFormLive();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSubmitForm));
        }
    }

    [RelayCommand]
    private async Task RegisterStaffAccountAsync()
    {
        if (IsBusy) return;
        ClearError();
        ValidateFormLive();

        if (!IsFormValid)
        {
            ErrorMessage = "Please fix the highlighted validation errors before submitting.";
            return;
        }

        if (!_sessionService.IsOwner)
        {
            ErrorMessage = "Only the Owner account is authorized to register staff accounts.";
            return;
        }

        try
        {
            IsBusy = true;
            OnPropertyChanged(nameof(CanSubmitForm));

            var effectiveRole = IsCustomRolePreset ? CustomRoleName.Trim() : SelectedRolePreset;
            var created = await _userManagementService.RegisterStaffAccountAsync(
                FullName,
                EmailAddress,
                Password,
                SecurityPin,
                effectiveRole,
                BuildCurrentPermissions());

            FullName = string.Empty;
            EmailAddress = string.Empty;
            Password = string.Empty;
            SecurityPin = string.Empty;
            SelectedRolePreset = UserRoles.Barista;

            var accounts = await _userManagementService.GetUserAccountsAsync();
            UserAccounts.Clear();
            foreach (var account in accounts)
                UserAccounts.Add(account);

            ValidateFormLive();

            await _dialogService.ShowAlertAsync(
                "Staff Account Created",
                $"Registered {created.FullName} ({created.EmailAddress}) with User Level '{created.RoleDisplayName}'.");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSubmitForm));
        }
    }

    [RelayCommand]
    private async Task ToggleAccountActiveStatusAsync(UserAccount? account)
    {
        if (account is null) return;

        if (account.IsOwner)
        {
            await _dialogService.ShowAlertAsync("Protected Account", "The primary Owner account cannot be deactivated.");
            return;
        }

        var nextState = !account.IsActive;
        var actionWord = nextState ? "Activate" : "Deactivate";

        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"{actionWord} Account",
            $"{actionWord} staff account for {account.FullName} ({account.EmailAddress})?",
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
