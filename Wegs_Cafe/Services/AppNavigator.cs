namespace Wegs_Cafe.Services;

/// <summary>Swaps the whole window between the login screen and the role-based Shell.</summary>
public class AppNavigator
{
    private readonly IServiceProvider _services;
    public AppNavigator(IServiceProvider services) => _services = services;

    private static Window? MainWindow => Application.Current?.Windows.FirstOrDefault();

    public void ShowLogin()
    {
        if (MainWindow != null) MainWindow.Page = _services.GetRequiredService<Views.LoginPage>();
    }

    public void ShowMain()
    {
        // A fresh Shell is built per sign-in so tabs reflect the new user's role.
        if (MainWindow != null) MainWindow.Page = _services.GetRequiredService<AppShell>();
    }
}
