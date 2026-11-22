using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Services;
using Wegs_Cafe.Views;

namespace Wegs_Cafe.ViewModels;

public partial class AppShellViewModel : ObservableObject
{
    private readonly SessionService _sessionService;
    private readonly ApplicationNavigationService _navigationService;
    private readonly IAuthenticationService _authenticationService;

    private string _activeRoute = nameof(PointOfSalePage);
    public string ActiveRoute
    {
        get => _activeRoute;
        set
        {
            if (SetProperty(ref _activeRoute, value))
            {
                OnPropertyChanged(nameof(IsPointOfSaleActive));
                OnPropertyChanged(nameof(IsProductRegistrationActive));
                OnPropertyChanged(nameof(IsInventoryManagementActive));
                OnPropertyChanged(nameof(IsUserManagementActive));
            }
        }
    }

    public bool IsPointOfSaleActive => string.Equals(ActiveRoute, nameof(PointOfSalePage), StringComparison.OrdinalIgnoreCase);
    public bool IsProductRegistrationActive => string.Equals(ActiveRoute, nameof(ProductRegistrationPage), StringComparison.OrdinalIgnoreCase);
    public bool IsInventoryManagementActive => string.Equals(ActiveRoute, nameof(InventoryManagementPage), StringComparison.OrdinalIgnoreCase);
    public bool IsUserManagementActive => string.Equals(ActiveRoute, nameof(UserManagementPage), StringComparison.OrdinalIgnoreCase);

    public bool CanAccessPointOfSale => _sessionService.CanAccessPointOfSale;
    public bool CanRegisterProducts => _sessionService.CanRegisterProducts;
    public bool CanViewInventory => _sessionService.CanViewInventory;
    public bool CanAccessUserManagement => _sessionService.CanAccessUserManagement;

    public string OperatorName => _sessionService.ActiveUser?.FullName ?? "Weg's Cafe Operator";
    public string RoleBadge => (_sessionService.ActiveUser?.RoleName ?? "Barista").ToUpperInvariant();

    public AppShellViewModel(
        SessionService sessionService,
        ApplicationNavigationService navigationService,
        IAuthenticationService authenticationService)
    {
        _sessionService = sessionService;
        _navigationService = navigationService;
        _authenticationService = authenticationService;

        _sessionService.ActiveUserChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanAccessPointOfSale));
            OnPropertyChanged(nameof(CanRegisterProducts));
            OnPropertyChanged(nameof(CanViewInventory));
            OnPropertyChanged(nameof(CanAccessUserManagement));
            OnPropertyChanged(nameof(OperatorName));
            OnPropertyChanged(nameof(RoleBadge));
        };
    }

    [RelayCommand]
    public async Task NavigateToRouteAsync(string? targetRoute)
    {
        if (string.IsNullOrWhiteSpace(targetRoute)) return;

        ActiveRoute = targetRoute;

        if (Shell.Current != null)
        {
            Shell.Current.FlyoutIsPresented = false;
            // Zero-animation instant route navigation to eliminate Android fragment slide lag on Helio G85
            await Shell.Current.GoToAsync($"//{targetRoute}", animate: false);
        }
    }

    [RelayCommand]
    public async Task SignOutAsync()
    {
        if (Shell.Current != null)
        {
            Shell.Current.FlyoutIsPresented = false;
        }
        await _authenticationService.SignOutAsync();
        _navigationService.NavigateToAuthentication();
    }
}
