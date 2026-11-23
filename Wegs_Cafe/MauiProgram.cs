using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Wegs_Cafe.Services;
using Wegs_Cafe.ViewModels;
using Wegs_Cafe.Views;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]

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

        // Core Services (SQLite Offline Cache + Firebase + Cloudinary)
        builder.Services.AddSingleton<LocalDatabaseService>();
        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<FirebaseService>();
        builder.Services.AddSingleton<CloudSynchronizationService>();
        builder.Services.AddSingleton<ApplicationNavigationService>();
        builder.Services.AddSingleton<IAuditTrailService, AuditTrailService>();

        builder.Services.AddSingleton<UserAccountStore>();
        builder.Services.AddSingleton<IAuthenticationService>(sp => sp.GetRequiredService<UserAccountStore>());
        builder.Services.AddSingleton<IUserManagementService>(sp => sp.GetRequiredService<UserAccountStore>());

        builder.Services.AddSingleton<IImageStorageService, CloudinaryProductImageStorageService>();

        builder.Services.AddSingleton<CatalogAndOrderStore>();
        builder.Services.AddSingleton<IProductManagementService>(sp => sp.GetRequiredService<CatalogAndOrderStore>());
        builder.Services.AddSingleton<IInventoryManagementService>(sp => sp.GetRequiredService<CatalogAndOrderStore>());
        builder.Services.AddSingleton<IOrderTransactionService>(sp => sp.GetRequiredService<CatalogAndOrderStore>());

        builder.Services.AddSingleton<OrderCartService>();
        builder.Services.AddSingleton<IReportExportService, ReportExportService>();
        builder.Services.AddSingleton<IReceiptPrinterService, ReceiptPrinterService>();
        builder.Services.AddSingleton<IReceiptDocumentService, ReceiptDocumentService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();

        // ViewModels (Singletons to leverage 8GB RAM for 0ms screen switching)
        builder.Services.AddSingleton<AppShellViewModel>();
        builder.Services.AddTransient<AuthenticationViewModel>();
        builder.Services.AddSingleton<SalesAnalyticsDashboardViewModel>();
        builder.Services.AddSingleton<ProductRegistrationViewModel>();
        builder.Services.AddSingleton<InventoryManagementViewModel>();
        builder.Services.AddSingleton<InventoryLogsViewModel>();
        builder.Services.AddSingleton<UserManagementViewModel>();
        builder.Services.AddSingleton<AuditTrailViewModel>();
        builder.Services.AddSingleton<PointOfSaleViewModel>();
        builder.Services.AddTransient<PaymentCheckoutViewModel>();
        builder.Services.AddTransient<ReceiptPreviewViewModel>();

        // Shell & Views (Pre-warmed Singletons in RAM)
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<AuthenticationPage>();
        builder.Services.AddSingleton<SalesAnalyticsDashboardPage>();
        builder.Services.AddSingleton<ProductRegistrationPage>();
        builder.Services.AddSingleton<InventoryManagementPage>();
        builder.Services.AddSingleton<InventoryLogsPage>();
        builder.Services.AddSingleton<UserManagementPage>();
        builder.Services.AddSingleton<AuditTrailPage>();
        builder.Services.AddSingleton<PointOfSalePage>();
        builder.Services.AddTransient<PaymentCheckoutPage>();
        builder.Services.AddTransient<ReceiptPreviewPage>();

        return builder.Build();
    }
}
