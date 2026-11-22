namespace Wegs_Cafe.Services;

public class DialogService : IDialogService
{
    private static Page? CurrentPage
    {
        get
        {
            var rootPage = Application.Current?.Windows.FirstOrDefault()?.Page;
            return rootPage is Shell shell ? shell.CurrentPage ?? rootPage : rootPage;
        }
    }

    public Task ShowAlertAsync(string title, string message)
        => CurrentPage?.DisplayAlertAsync(title, message, "OK") ?? Task.CompletedTask;

    public async Task<bool> ShowConfirmationAsync(string title, string message, string acceptText = "Yes", string cancelText = "Cancel")
        => CurrentPage != null && await CurrentPage.DisplayAlertAsync(title, message, acceptText, cancelText);

    public Task<string?> ShowPromptAsync(string title, string message, string placeholder = "", bool numeric = false, string initialValue = "")
        => CurrentPage?.DisplayPromptAsync(
               title,
               message,
               "OK",
               "Cancel",
               placeholder,
               12,
               numeric ? Keyboard.Numeric : Keyboard.Default,
               initialValue)
           ?? Task.FromResult<string?>(null);

    public async Task<string?> ShowActionSheetAsync(string title, string cancelText, params string[] actionButtons)
    {
        if (CurrentPage == null) return null;
        var selection = await CurrentPage.DisplayActionSheetAsync(title, cancelText, null, actionButtons);
        return selection == cancelText ? null : selection;
    }
}
