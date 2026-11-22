using CommunityToolkit.Mvvm.ComponentModel;

namespace Wegs_Cafe.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string _pageTitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string _errorMessage = string.Empty;

    public bool IsNotBusy => !IsBusy;
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    protected void ClearError() => ErrorMessage = string.Empty;
}
