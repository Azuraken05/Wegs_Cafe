using Microsoft.Extensions.Logging;
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
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG

            builder.Logging.AddDebug();
#endif
            // ---------- Services ----------
            builder.Services.AddSingleton<FirebaseService>();
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<MenuService>();
            builder.Services.AddSingleton<OrderService>();
            builder.Services.AddSingleton<CartService>();

            // ---------- ViewModels ----------
            // Register here as you create them, e.g.:
            // builder.Services.AddTransient<ViewModels.MenuViewModel>();

            // ---------- Pages ----------
            // Shell resolves registered pages from DI, so constructor injection works.
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<MenuPage>();
            builder.Services.AddTransient<AboutPage>();
            builder.Services.AddTransient<ContactPage>();
            builder.Services.AddTransient<LoginPage>();

            return builder.Build();
        }
    }
}
