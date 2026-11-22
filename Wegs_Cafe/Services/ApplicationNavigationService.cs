namespace Wegs_Cafe.Services;

/// <summary>
/// Manages root window transitions between AuthenticationPage and the Hamburger Sidebar AppShell.
/// </summary>
public class ApplicationNavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public ApplicationNavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private static Window? CurrentWindow => Application.Current?.Windows.FirstOrDefault();

    public void NavigateToAuthentication()
    {
        if (CurrentWindow != null)
            CurrentWindow.Page = _serviceProvider.GetRequiredService<Views.AuthenticationPage>();
    }

    public void NavigateToMainShell()
    {
        if (CurrentWindow != null)
            CurrentWindow.Page = _serviceProvider.GetRequiredService<AppShell>();
    }
}
