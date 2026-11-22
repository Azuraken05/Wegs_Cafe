using Wegs_Cafe.Helpers;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class UserManagementPage : ContentPage
{
    private readonly UserManagementViewModel _viewModel;

    public UserManagementPage(UserManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadUserAccountsCommand.ExecuteAsync(null);
        await AnimationHelper.AnimateEntranceAsync(UserManagementContainer);
    }
}
