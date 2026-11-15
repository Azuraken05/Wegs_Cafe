using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _auth;
    private readonly AppNavigator _navigator;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;

    public LoginViewModel(IAuthService auth, AppNavigator navigator)
    {
        _auth = auth;
        _navigator = navigator;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        ErrorMessage = string.Empty;
        try
        {
            IsBusy = true;
            await _auth.LoginAsync(Username, Password);
            Password = string.Empty;
            _navigator.ShowMain();
        }
        catch (AuthException ex) { ErrorMessage = ex.Message; }
        catch (Exception) { ErrorMessage = "Something went wrong. Please try again."; }
        finally { IsBusy = false; }
    }
}
