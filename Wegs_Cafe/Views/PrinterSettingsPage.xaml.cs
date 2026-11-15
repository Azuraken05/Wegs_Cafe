using Wegs_Cafe.Services;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class PrinterSettingsPage : ContentPage
{
    private readonly PrinterSettingsViewModel _vm;

    public PrinterSettingsPage(PrinterSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
    }

    private void OnSelectClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is PrinterDevice d)
            _vm.SelectCommand.Execute(d);
    }
}
