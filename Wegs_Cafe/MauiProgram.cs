using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Wegs_Cafe.Services;
using Wegs_Cafe.Views;

namespace Wegs_Cafe
{
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
                    // Placeholders: swap for Poppins-Bold / Inter files in Resources/Fonts
                    // (keep the aliases "Heading" and "Body" - the styles use them).
                    fonts.AddFont("OpenSans-Semibold.ttf", "Heading");
                    fonts.AddFont("OpenSans-Regular.ttf", "Body");
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif
            // ---------- Services ----------
            // FirebaseService is intentionally NOT registered yet (Firebase comes last).
            builder.Services.AddSingleton<SessionService>();
            builder.Services.AddSingleton<AppNavigator>();
            builder.Services.AddSingleton<IAuditService, LocalAuditService>();
            builder.Services.AddSingleton<LocalUserStore>();
            builder.Services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<LocalUserStore>());
            builder.Services.AddSingleton<IUserService>(sp => sp.GetRequiredService<LocalUserStore>());
            builder.Services.AddSingleton<IImageStorageService, LocalImageStorage>();
            builder.Services.AddSingleton<LocalCatalogStore>();
            builder.Services.AddSingleton<IProductService>(sp => sp.GetRequiredService<LocalCatalogStore>());
            builder.Services.AddSingleton<IInventoryService>(sp => sp.GetRequiredService<LocalCatalogStore>());
            builder.Services.AddSingleton<IOrderService>(sp => sp.GetRequiredService<LocalCatalogStore>());
            builder.Services.AddSingleton<IShiftService>(sp => sp.GetRequiredService<LocalCatalogStore>());
            builder.Services.AddSingleton<CartService>();
            builder.Services.AddSingleton<IReceiptPrinterService, BluetoothPrinterService>();
            builder.Services.AddSingleton<IReceiptDocumentService, ReceiptDocumentService>();
            builder.Services.AddSingleton<IDialogService, DialogService>();

            // ---------- ViewModels ----------
            builder.Services.AddTransient<ViewModels.LoginViewModel>();
            builder.Services.AddTransient<ViewModels.StaffViewModel>();
            builder.Services.AddTransient<ViewModels.ProductsViewModel>();
            builder.Services.AddTransient<ViewModels.ProductEditViewModel>();
            builder.Services.AddTransient<ViewModels.InventoryViewModel>();
            builder.Services.AddTransient<ViewModels.AuditLogViewModel>();
            builder.Services.AddTransient<ViewModels.PosViewModel>();
            builder.Services.AddTransient<ViewModels.PaymentViewModel>();
            builder.Services.AddTransient<ViewModels.ReceiptViewModel>();
            builder.Services.AddTransient<ViewModels.OrderHistoryViewModel>();
            builder.Services.AddTransient<ViewModels.ShiftViewModel>();
            builder.Services.AddTransient<ViewModels.PrinterSettingsViewModel>();
            builder.Services.AddTransient<ViewModels.DashboardViewModel>();

            // ---------- Shell & Pages ----------
            builder.Services.AddTransient<AppShell>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<PosPage>();
            builder.Services.AddTransient<PaymentPage>();
            builder.Services.AddTransient<ReceiptPage>();
            builder.Services.AddTransient<OrderHistoryPage>();
            builder.Services.AddTransient<ShiftPage>();
            builder.Services.AddTransient<PrinterSettingsPage>();
            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<ProductsPage>();
            builder.Services.AddTransient<InventoryPage>();
            builder.Services.AddTransient<StaffPage>();
            builder.Services.AddTransient<AuditLogPage>();
            builder.Services.AddTransient<ProductEditPage>();

            return builder.Build();
        }
    }
}
