using Wegs_Cafe.Helpers;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class AuthenticationPage : ContentPage
{
    public AuthenticationPage(AuthenticationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await AnimationHelper.AnimateEntranceAsync(AuthenticationCard);
    }
}
