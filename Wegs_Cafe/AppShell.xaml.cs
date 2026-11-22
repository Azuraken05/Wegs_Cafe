using Wegs_Cafe.Services;
using Wegs_Cafe.Views;

namespace Wegs_Cafe;

public partial class AppShell : Shell
{
    private readonly SessionService _sessionService;
    private readonly IAuthenticationService _authenticationService;
    private readonly ApplicationNavigationService _navigationService;

    public AppShell(
        SessionService sessionService,
        IAuthenticationService authenticationService,
        ApplicationNavigationService navigationService)
    {
        InitializeComponent();

        _sessionService = sessionService;
        _authenticationService = authenticationService;
        _navigationService = navigationService;

        Routing.RegisterRoute(nameof(PaymentCheckoutPage), typeof(PaymentCheckoutPage));
        Routing.RegisterRoute(nameof(ReceiptPreviewPage), typeof(ReceiptPreviewPage));

        ApplyRoleBasedNavigationGuards();
        BuildSidebarHeader();
    }

    private void ApplyRoleBasedNavigationGuards()
    {
        PointOfSaleFlyoutItem.IsVisible = _sessionService.CanAccessPointOfSale;
        ProductRegistrationFlyoutItem.IsVisible = _sessionService.CanRegisterProducts;
        InventoryManagementFlyoutItem.IsVisible = _sessionService.CanViewInventory;
        UserManagementFlyoutItem.IsVisible = _sessionService.CanAccessUserManagement;

        // Default landing screen for both Owner and Barista is PointOfSalePage for immediate counter readiness
        CurrentItem = _sessionService.CanAccessPointOfSale
            ? PointOfSaleFlyoutItem
            : InventoryManagementFlyoutItem;
    }

    private void BuildSidebarHeader()
    {
        var activeUser = _sessionService.ActiveUser;
        var fullName = activeUser?.FullName ?? "Weg's Cafe Operator";
        var roleBadge = (activeUser?.RoleName ?? "Barista").ToUpperInvariant();

        FlyoutHeader = new VerticalStackLayout
        {
            Padding = new Thickness(20, 28, 20, 18),
            Spacing = 6,
            BackgroundColor = Color.FromArgb("#0A0A0A"),
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Image
                        {
                            Source = "wegs_logo.png",
                            WidthRequest = 44,
                            HeightRequest = 44,
                            Aspect = Aspect.AspectFit,
                            VerticalOptions = LayoutOptions.Center
                        },
                        new VerticalStackLayout
                        {
                            Spacing = 2,
                            VerticalOptions = LayoutOptions.Center,
                            Children =
                            {
                                new Label
                                {
                                    Text = "WEG'S CAFE",
                                    TextColor = Colors.White,
                                    FontSize = 20,
                                    FontFamily = "QuicksandBold"
                                },
                                new Label
                                {
                                    Text = fullName,
                                    TextColor = Color.FromArgb("#F7F6F2"),
                                    FontSize = 13,
                                    FontFamily = "InterSemiBold"
                                }
                            }
                        }
                    }
                },
                new Border
                {
                    BackgroundColor = Color.FromArgb("#1C1C1E"),
                    Stroke = Color.FromArgb("#6E6E73"),
                    StrokeThickness = 1,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(10, 4),
                    HorizontalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0, 6, 0, 0),
                    Content = new Label
                    {
                        Text = $"USER LEVEL: {roleBadge}",
                        TextColor = Colors.White,
                        FontSize = 11,
                        FontFamily = "QuicksandBold"
                    }
                }
            }
        };
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        FlyoutIsPresented = false;
        await _authenticationService.SignOutAsync();
        _navigationService.NavigateToAuthentication();
    }
}
