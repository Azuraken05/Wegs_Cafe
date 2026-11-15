using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class ReceiptViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IReceiptPrinterService _printer;
    private readonly IReceiptDocumentService _documents;
    private readonly IDialogService _dialogs;
    private Order? _order;

    [ObservableProperty] private string _previewText = string.Empty;
    [ObservableProperty] private string _title2 = "Receipt";
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ReceiptViewModel(IReceiptPrinterService printer, IReceiptDocumentService documents, IDialogService dialogs)
    {
        _printer = printer;
        _documents = documents;
        _dialogs = dialogs;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("order", out var o) && o is Order order)
        {
            _order = order;
            Title2 = order.ReceiptNumber;
            PreviewText = ReceiptBuilder.ToPlainText(ReceiptBuilder.Build(order, _printer.CharsPerLine));
        }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (_order == null || IsBusy) return;
        if (!_printer.IsSupported) { await _dialogs.AlertAsync("Printer", "Bluetooth printing works on Android only. Use Share PDF instead."); return; }
        if (_printer.SelectedPrinter == null) { await _dialogs.AlertAsync("No printer", "No printer is set up yet. The owner can pair and choose one in the Printer tab. You can still share the PDF."); return; }
        try
        {
            IsBusy = true; StatusMessage = "Printing…";
            await _printer.PrintAsync(_order);
            StatusMessage = "Sent to printer.";
        }
        catch (Exception ex) { StatusMessage = string.Empty; await _dialogs.AlertAsync("Print failed", ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SharePdfAsync()
    {
        if (_order == null) return;
        try
        {
            var path = await _documents.CreatePdfAsync(_order);
            if (path == null) { await ShareTextAsync(); return; }     // platform can't make PDFs -> share text
            await Share.Default.RequestAsync(new ShareFileRequest { Title = _order.ReceiptNumber, File = new ShareFile(path, "application/pdf") });
        }
        catch (Exception ex) { await _dialogs.AlertAsync("Share failed", ex.Message); }
    }

    [RelayCommand]
    private Task ShareTextAsync()
        => _order == null ? Task.CompletedTask
            : Share.Default.RequestAsync(new ShareTextRequest { Title = _order.ReceiptNumber, Text = PreviewText });

    [RelayCommand] private Task DoneAsync() => Shell.Current.GoToAsync("..");
}
