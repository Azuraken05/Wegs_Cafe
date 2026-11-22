using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;
using Wegs_Cafe.Views;

namespace Wegs_Cafe.ViewModels;

public partial class ReceiptPreviewViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IReceiptPrinterService _receiptPrinterService;
    private readonly IReceiptDocumentService _receiptDocumentService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private Order? _completedOrder;

    [ObservableProperty]
    private string _formattedReceiptText = string.Empty;

    [ObservableProperty]
    private string _printerDeviceSummary = string.Empty;

    public ReceiptPreviewViewModel(
        IReceiptPrinterService receiptPrinterService,
        IReceiptDocumentService receiptDocumentService,
        IDialogService dialogService)
    {
        _receiptPrinterService = receiptPrinterService;
        _receiptDocumentService = receiptDocumentService;
        _dialogService = dialogService;
        PageTitle = "Official Receipt & Thermal Print";
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("CompletedOrder", out var orderObj) && orderObj is Order order)
        {
            CompletedOrder = order;
            FormattedReceiptText = ReceiptFormatter.FormatPlainTextReceipt(order, _receiptPrinterService.CharactersPerLine);
            var activePrinter = _receiptPrinterService.SelectedPrinter;
            PrinterDeviceSummary = activePrinter != null
                ? $"{activePrinter.DeviceName} ({_receiptPrinterService.PaperWidthMillimeters}mm / {_receiptPrinterService.CharactersPerLine} chars)"
                : $"Standard {_receiptPrinterService.PaperWidthMillimeters}mm Thermal Format";
        }
    }

    [RelayCommand]
    private async Task PrintThermalReceiptAsync()
    {
        if (CompletedOrder is null || IsBusy) return;

        try
        {
            IsBusy = true;
            await _receiptPrinterService.PrintOrderReceiptAsync(CompletedOrder);
            await _dialogService.ShowAlertAsync(
                "Thermal Receipt Printed",
                $"Sent ESC/POS receipt {CompletedOrder.ReceiptNumber} to {_receiptPrinterService.SelectedPrinter?.DeviceName ?? "Thermal Printer"}.");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Printer Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAndShareReceiptDocumentAsync()
    {
        if (CompletedOrder is null || IsBusy) return;

        try
        {
            IsBusy = true;
            var filePath = await _receiptDocumentService.GenerateReceiptPdfAsync(CompletedOrder);
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = $"Receipt {CompletedOrder.ReceiptNumber}",
                    File = new ShareFile(filePath)
                });
            }
            catch
            {
                await _dialogService.ShowAlertAsync("Receipt Saved", $"Saved receipt document to:\n{filePath}");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Export Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartNewOrderAsync()
    {
        await Shell.Current.GoToAsync($"//{nameof(PointOfSalePage)}");
    }
}
