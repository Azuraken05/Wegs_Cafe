using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class ShiftViewModel : BaseViewModel
{
    private readonly IShiftService _shifts;
    private readonly IDialogService _dialogs;
    private Shift? _shift;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(NoOpenShift))] private bool _hasOpenShift;
    [ObservableProperty] private string _openingCashText = string.Empty;
    [ObservableProperty] private string _actualCashText = string.Empty;
    [ObservableProperty] private string _shiftInfo = string.Empty;
    [ObservableProperty] private ShiftReading _reading = new();
    [ObservableProperty] private string _zReport = string.Empty;
    [ObservableProperty] private bool _hasZReport;

    public bool NoOpenShift => !HasOpenShift;

    public ShiftViewModel(IShiftService shifts, IDialogService dialogs)
    {
        _shifts = shifts;
        _dialogs = dialogs;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            _shift = await _shifts.GetOpenShiftAsync();
            HasOpenShift = _shift != null;
            if (_shift != null)
            {
                Reading = await _shifts.GetReadingAsync(_shift);
                ShiftInfo = $"Opened by {_shift.OpenedByName} · {IsoDate.ToLocal(_shift.OpenedAt):MMM d, h:mm tt}";
                HasZReport = false;
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        ErrorMessage = string.Empty;
        if (!TryMoney(OpeningCashText, out var cash)) { ErrorMessage = "Enter the opening cash amount (0 if none)."; return; }
        try
        {
            await _shifts.OpenShiftAsync(cash);
            OpeningCashText = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex) when (ex is ValidationException) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        ErrorMessage = string.Empty;
        if (!TryMoney(ActualCashText, out var actual)) { ErrorMessage = "Count the drawer and enter the actual cash."; return; }
        var expected = Reading.ExpectedCash;
        var diff = actual - expected;
        var msg = diff == 0 ? "The drawer matches the system."
            : $"The drawer is {(diff < 0 ? "SHORT" : "OVER")} by ₱{Math.Abs(diff):N2}. This will be flagged in the audit log.";
        if (!await _dialogs.ConfirmAsync("Close shift (Z reading)", $"Expected ₱{expected:N2}, counted ₱{actual:N2}.\n{msg}\n\nClose the shift?", "Close shift")) return;
        try
        {
            var closed = await _shifts.CloseShiftAsync(actual);
            ZReport = BuildZ(closed);
            HasZReport = true;
            HasOpenShift = false;
            ActualCashText = string.Empty;
        }
        catch (Exception ex) when (ex is ValidationException) { ErrorMessage = ex.Message; }
    }

    private static string BuildZ(Shift s)
    {
        var diff = s.Difference == 0 ? "Balanced" : $"{(s.Difference < 0 ? "SHORT" : "OVER")} ₱{Math.Abs(s.Difference):N2}";
        return $"Z READING — shift closed\n" +
               $"Opened: {IsoDate.ToLocal(s.OpenedAt):MMM d, h:mm tt} ({s.OpenedByName})\n" +
               $"Closed: {IsoDate.ToLocal(s.ClosedAt):MMM d, h:mm tt} ({s.ClosedByName})\n\n" +
               $"Orders: {s.OrderCount}\nTotal sales: ₱{s.TotalSales:N2}\n  Cash: ₱{s.CashSales:N2}\n  GCash: ₱{s.GCashSales:N2}\n  Card: ₱{s.CardSales:N2}\n\n" +
               $"Opening cash: ₱{s.OpeningCash:N2}\nExpected cash: ₱{s.ExpectedCash:N2}\nCounted cash: ₱{s.ActualCash:N2}\nResult: {diff}";
    }

    private static bool TryMoney(string s, out decimal v)
        => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out v) && v >= 0;
}
