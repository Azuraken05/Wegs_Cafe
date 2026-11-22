using Wegs_Cafe.Services;
using Wegs_Cafe.ViewModels;
using Wegs_Cafe.Views;

namespace Wegs_Cafe;

public partial class AppShell : Shell
{
    private readonly AppShellViewModel _viewModel;
    private readonly SessionService _sessionService;
    private readonly IAuthenticationService _authenticationService;
    private readonly ApplicationNavigationService _navigationService;

    public AppShell(
        AppShellViewModel viewModel,
        SessionService sessionService,
        IAuthenticationService authenticationService,
        ApplicationNavigationService navigationService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _sessionService = sessionService;
        _authenticationService = authenticationService;
        _navigationService = navigationService;
        BindingContext = _viewModel;

        Routing.RegisterRoute(nameof(PaymentCheckoutPage), typeof(PaymentCheckoutPage));
        Routing.RegisterRoute(nameof(ReceiptPreviewPage), typeof(ReceiptPreviewPage));
        Routing.RegisterRoute(nameof(PointOfSalePage), typeof(PointOfSalePage));
        Routing.RegisterRoute(nameof(ProductRegistrationPage), typeof(ProductRegistrationPage));
        Routing.RegisterRoute(nameof(InventoryManagementPage), typeof(InventoryManagementPage));
        Routing.RegisterRoute(nameof(UserManagementPage), typeof(UserManagementPage));

        Navigated += OnShellNavigated;

        ApplyRoleBasedNavigationGuards();
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        var currentRoute = CurrentState?.Location?.ToString() ?? string.Empty;
        var pageName = currentRoute
            .Split(new[] { '/', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? string.Empty;
        if (!string.IsNullOrEmpty(pageName))
        {
            _viewModel.ActiveRoute = pageName;
        }
    }

    private void ApplyRoleBasedNavigationGuards()
    {
        // Default landing screen for both Owner and Barista is PointOfSalePage for immediate counter readiness
        var initialItem = _sessionService.CanAccessPointOfSale
            ? PointOfSaleFlyoutItem
            : InventoryManagementFlyoutItem;
        CurrentItem = initialItem;
        _viewModel.ActiveRoute = _sessionService.CanAccessPointOfSale
            ? nameof(PointOfSalePage)
            : nameof(InventoryManagementPage);
    }
}
