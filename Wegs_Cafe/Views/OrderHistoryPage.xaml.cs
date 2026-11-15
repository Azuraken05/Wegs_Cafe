using Wegs_Cafe.Models;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class OrderHistoryPage : ContentPage
{
    private readonly OrderHistoryViewModel _vm;

    public OrderHistoryPage(OrderHistoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    private void OnViewClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is Order o) _vm.ViewCommand.Execute(o);
    }

    private void OnVoidClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is Order o) _vm.VoidCommand.Execute(o);
    }
}
