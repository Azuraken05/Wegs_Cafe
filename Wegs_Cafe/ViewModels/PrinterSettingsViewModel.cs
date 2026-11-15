using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class PrinterSettingsViewModel : BaseViewModel
{
    private readonly IReceiptPrinterService _printer;
    private readonly IDialogService _dialogs;

    public ObservableCollection<PrinterDevice> Devices { get; } = new();
    public bool IsSupported => _printer.IsSupported;

    [ObservableProperty] private string _selectedName = "None";
    [ObservableProperty] private bool _is58 = true;
    [ObservableProperty] private bool _is80;

    public PrinterSettingsViewModel(IReceiptPrinterService printer, IDialogService dialogs)
    {
        _printer = printer;
        _dialogs = dialogs;
        Refresh();
    }

    private void Refresh()
    {
        SelectedName = _printer.SelectedPrinter?.Name ?? "None";
        Is58 = _printer.PaperWidthMm == 58;
        Is80 = _printer.PaperWidthMm == 80;
    }

    [RelayCommand] private void SetWidth(string mm) { _printer.PaperWidthMm = mm == "80" ? 80 : 58; Refresh(); }

    [RelayCommand]
    private async Task ScanAsync()
    {
        try
        {
            IsBusy = true;
            Devices.Clear();
            foreach (var d in await _printer.GetPairedPrintersAsync()) Devices.Add(d);
            if (Devices.Count == 0) await _dialogs.AlertAsync("No paired devices", "Pair your thermal printer in Android Bluetooth settings first, then scan again.");
        }
        catch (Exception ex) { await _dialogs.AlertAsync("Bluetooth", ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void Select(PrinterDevice? d) { if (d == null) return; _printer.SelectPrinter(d); Refresh(); }
    [RelayCommand] private void Forget() { _printer.SelectPrinter(null); Refresh(); }

    [RelayCommand]
    private async Task TestAsync()
    {
        try { IsBusy = true; await _printer.PrintTestAsync(); }
        catch (Exception ex) { await _dialogs.AlertAsync("Print failed", ex.Message); }
        finally { IsBusy = false; }
    }
}
