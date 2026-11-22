using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Wegs_Cafe.Services;
using Wegs_Cafe.ViewModels;
using Wegs_Cafe.Views;

namespace Wegs_Cafe;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Semibold.ttf", "QuicksandBold");
                fonts.AddFont("OpenSans-Regular.ttf", "InterRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "InterSemiBold");
                fonts.AddFont("OpenSans-Semibold.ttf", "Heading");
                fonts.AddFont("OpenSans-Regular.ttf", "Body");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Core Services (FirebaseService + Offline-First Local Stores)
        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<FirebaseService>();
        builder.Services.AddSingleton<ApplicationNavigationService>();
        builder.Services.AddSingleton<IAuditTrailService, AuditTrailService>();

        builder.Services.AddSingleton<UserAccountStore>();
        builder.Services.AddSingleton<IAuthenticationService>(sp => sp.GetRequiredService<UserAccountStore>());
        builder.Services.AddSingleton<IUserManagementService>(sp => sp.GetRequiredService<UserAccountStore>());

        builder.Services.AddSingleton<IImageStorageService, ImageStorageService>();

        builder.Services.AddSingleton<CatalogAndOrderStore>();
        builder.Services.AddSingleton<IProductManagementService>(sp => sp.GetRequiredService<CatalogAndOrderStore>());
        builder.Services.AddSingleton<IInventoryManagementService>(sp => sp.GetRequiredService<CatalogAndOrderStore>());
        builder.Services.AddSingleton<IOrderTransactionService>(sp => sp.GetRequiredService<CatalogAndOrderStore>());

        builder.Services.AddSingleton<OrderCartService>();
        builder.Services.AddSingleton<IReportExportService, ReportExportService>();
        builder.Services.AddSingleton<IReceiptPrinterService, ReceiptPrinterService>();
        builder.Services.AddSingleton<IReceiptDocumentService, ReceiptDocumentService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();

        // ViewModels
        builder.Services.AddTransient<AuthenticationViewModel>();
        builder.Services.AddTransient<SalesAnalyticsDashboardViewModel>();
        builder.Services.AddTransient<ProductRegistrationViewModel>();
        builder.Services.AddTransient<InventoryManagementViewModel>();
        builder.Services.AddTransient<UserManagementViewModel>();
        builder.Services.AddTransient<AuditTrailViewModel>();
        builder.Services.AddTransient<PointOfSaleViewModel>();
        builder.Services.AddTransient<PaymentCheckoutViewModel>();
        builder.Services.AddTransient<ReceiptPreviewViewModel>();

        // Shell & Views
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<AuthenticationPage>();
        builder.Services.AddTransient<SalesAnalyticsDashboardPage>();
        builder.Services.AddTransient<ProductRegistrationPage>();
        builder.Services.AddTransient<InventoryManagementPage>();
        builder.Services.AddTransient<UserManagementPage>();
        builder.Services.AddTransient<AuditTrailPage>();
        builder.Services.AddTransient<PointOfSalePage>();
        builder.Services.AddTransient<PaymentCheckoutPage>();
        builder.Services.AddTransient<ReceiptPreviewPage>();

        return builder.Build();
    }
}
