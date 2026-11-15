namespace Wegs_Cafe.Services;

public class DialogService : IDialogService
{
    private static Page? CurrentPage
    {
        get
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            return page is Shell shell ? shell.CurrentPage ?? page : page;
        }
    }

    public Task AlertAsync(string title, string message)
        => CurrentPage?.DisplayAlertAsync(title, message, "OK") ?? Task.CompletedTask;

    public async Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "Cancel")
        => CurrentPage != null && await CurrentPage.DisplayAlertAsync(title, message, accept, cancel);

    public Task<string?> PromptAsync(string title, string message, string placeholder = "", bool numeric = false, string initial = "")
        => CurrentPage?.DisplayPromptAsync(title, message, "OK", "Cancel", placeholder, 9,
               numeric ? Keyboard.Numeric : Keyboard.Default, initial) ?? Task.FromResult<string?>(null);

    public async Task<string?> ActionSheetAsync(string title, string cancel, params string[] buttons)
    {
        if (CurrentPage == null) return null;
        var result = await CurrentPage.DisplayActionSheetAsync(title, cancel, null, buttons);
        return result == cancel ? null : result;
    }
}
