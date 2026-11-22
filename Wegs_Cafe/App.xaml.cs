using Wegs_Cafe.Views;

namespace Wegs_Cafe;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
        _serviceProvider = serviceProvider;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_serviceProvider.GetRequiredService<AuthenticationPage>());
    }
}
