using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class AuthenticationViewModel : BaseViewModel
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ApplicationNavigationService _navigationService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _securityPin = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPasswordMode))]
    [NotifyPropertyChangedFor(nameof(ModeToggleButtonText))]
    private bool _isPinQuickSwitchMode;

    public bool IsPasswordMode => !IsPinQuickSwitchMode;
    public string ModeToggleButtonText => IsPinQuickSwitchMode
        ? "Switch to Username & Password Sign-In"
        : "Switch to Cashier PIN Quick-Switch";

    public AuthenticationViewModel(
        IAuthenticationService authenticationService,
        ApplicationNavigationService navigationService)
    {
        _authenticationService = authenticationService;
        _navigationService = navigationService;
        PageTitle = "Sign In — Weg's Cafe";
    }

    [RelayCommand]
    private void ToggleSignInMode()
    {
        ClearError();
        SecurityPin = string.Empty;
        IsPinQuickSwitchMode = !IsPinQuickSwitchMode;
    }

    [RelayCommand]
    private void FillDemoOwnerCredentials()
    {
        ClearError();
        IsPinQuickSwitchMode = false;
        Username = LocalOwnerSeed.OwnerUsername;
        Password = LocalOwnerSeed.OwnerPassword;
    }

    [RelayCommand]
    private void AppendPinDigit(string digit)
    {
        if (SecurityPin.Length >= 6) return;
        ClearError();
        SecurityPin += digit;
    }

    [RelayCommand]
    private void ClearPinDigits()
    {
        ClearError();
        SecurityPin = string.Empty;
    }

    [RelayCommand]
    private void BackspacePinDigit()
    {
        if (SecurityPin.Length == 0) return;
        SecurityPin = SecurityPin[..^1];
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy) return;
        ClearError();

        try
        {
            IsBusy = true;

            if (IsPinQuickSwitchMode)
            {
                await _authenticationService.SignInWithPinAsync(SecurityPin);
            }
            else
            {
                await _authenticationService.SignInAsync(Username, Password);
            }

            Password = string.Empty;
            SecurityPin = string.Empty;
            _navigationService.NavigateToMainShell();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
