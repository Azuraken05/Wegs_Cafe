using CommunityToolkit.Mvvm.ComponentModel;

namespace Wegs_Cafe.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool IsNotBusy => !IsBusy;
}
