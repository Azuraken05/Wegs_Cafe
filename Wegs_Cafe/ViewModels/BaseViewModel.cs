using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wegs_Cafe.ViewModels;

/// <summary>
/// Base class for all ViewModels. Provides property-change notification,
/// IsBusy/Title, and a safe way to run async work with error alerts.
/// </summary>
public abstract class BaseViewModel : INotifyPropertyChanged
{
    private bool _isBusy;
    private string _title = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Runs an async action with IsBusy handling and shows an alert on error.</summary>
    protected async Task RunSafeAsync(Func<Task> action, Func<Exception, string>? errorMessage = null)
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            await action();
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Error", errorMessage?.Invoke(ex) ?? ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected static Task ShowAlertAsync(string title, string message)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        return page?.DisplayAlertAsync(title, message, "OK") ?? Task.CompletedTask;
    }
}
