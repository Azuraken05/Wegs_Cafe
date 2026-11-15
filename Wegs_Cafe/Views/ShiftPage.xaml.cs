using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class ShiftPage : ContentPage
{
    private readonly ShiftViewModel _vm;

    public ShiftPage(ShiftViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
