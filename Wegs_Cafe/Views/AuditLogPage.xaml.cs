using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class AuditLogPage : ContentPage
{
    private readonly AuditLogViewModel _vm;

    public AuditLogPage(AuditLogViewModel viewModel)
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
