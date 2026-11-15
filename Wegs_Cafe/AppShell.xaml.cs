using Wegs_Cafe.Services;

namespace Wegs_Cafe;

public partial class AppShell : Shell
{
    private readonly IAuthService _auth;
    private readonly AppNavigator _navigator;

    public AppShell(SessionService session, IAuthService auth, AppNavigator navigator)
    {
        InitializeComponent();
        Routing.RegisterRoute("productedit", typeof(Views.ProductEditPage));
        Routing.RegisterRoute("payment", typeof(Views.PaymentPage));
        Routing.RegisterRoute("receipt", typeof(Views.ReceiptPage));
        _auth = auth;
        _navigator = navigator;

        // Role-based navigation: hide what this user may not open.
        DashboardTab.IsVisible = session.CanViewDashboard;
        PosTab.IsVisible = session.CanUsePos;
        OrdersTab.IsVisible = session.CanUsePos;
        ShiftTab.IsVisible = session.CanUsePos;
        ProductsTab.IsVisible = session.CanManageProducts;
        InventoryTab.IsVisible = session.CanViewInventory;
        StaffTab.IsVisible = session.CanManageUsers;
        AuditTab.IsVisible = session.CanViewAuditLogs;
        PrinterTab.IsVisible = session.CanConfigurePrinter;

        // Select the first landing page: Dashboard for Owner/Manager, POS for Staff.
        CurrentItem = session.CanViewDashboard ? DashboardTab : PosTab;

        // Hamburger Menu Sidebar navigation on all devices.
        FlyoutBehavior = FlyoutBehavior.Flyout;

        FlyoutHeader = new VerticalStackLayout
        {
            Padding = new Thickness(20, 32, 20, 18),
            Spacing = 4,
            BackgroundColor = Color.FromArgb("#0A0A0A"),
            Children =
            {
                new Label { Text = "WEG'S CAFE", TextColor = Colors.White, FontSize = 22, FontFamily = "Heading" },
                new Label { Text = session.DisplayName, TextColor = Color.FromArgb("#F7F6F2"), FontSize = 15, FontFamily = "Heading" },
                new Border
                {
                    BackgroundColor = Color.FromArgb("#1C1C1E"),
                    Stroke = Color.FromArgb("#6E6E73"),
                    StrokeThickness = 1,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(10, 3),
                    HorizontalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0, 4, 0, 0),
                    Content = new Label
                    {
                        Text = (session.CurrentUser?.RoleDisplay ?? "").ToUpperInvariant(),
                        TextColor = Color.FromArgb("#E2E2E0"),
                        FontSize = 11,
                        FontFamily = "Heading"
                    }
                },
                new BoxView { HeightRequest = 1, Color = Color.FromArgb("#1C1C1E"), Margin = new Thickness(0, 12, 0, 0) }
            }
        };
    }

    private void OnLogoutClicked(object? sender, EventArgs e)
    {
        FlyoutIsPresented = false;
        _auth.Logout();
        _navigator.ShowLogin();
    }
}
