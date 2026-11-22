using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;
using Wegs_Cafe.Views;

namespace Wegs_Cafe.ViewModels;

public partial class PaymentCheckoutViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IOrderTransactionService _orderTransactionService;
    private readonly OrderCartService _orderCartService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _orderType = OrderTypes.DineIn;

    [ObservableProperty]
    private string _customerOrTableLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCashSelected))]
    [NotifyPropertyChangedFor(nameof(IsGCashSelected))]
    [NotifyPropertyChangedFor(nameof(IsCreditCardSelected))]
    [NotifyPropertyChangedFor(nameof(RequiresReferenceNumber))]
    [NotifyPropertyChangedFor(nameof(ReferenceFieldTitle))]
    [NotifyPropertyChangedFor(nameof(ChangeDueAmount))]
    private string _selectedPaymentMethod = PaymentMethods.Cash;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtotalAmount))]
    [NotifyPropertyChangedFor(nameof(TotalPayableAmount))]
    [NotifyPropertyChangedFor(nameof(ChangeDueAmount))]
    private string _discountAmountText = "0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountTenderedValue))]
    [NotifyPropertyChangedFor(nameof(ChangeDueAmount))]
    private string _amountTenderedText = string.Empty;

    [ObservableProperty]
    private string _paymentReferenceNumber = string.Empty;

    // Inline Validation Errors
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomerOrTableError))]
    private string _customerOrTableError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAmountTenderedError))]
    private string _amountTenderedError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReferenceNumberError))]
    private string _referenceNumberError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCompletePayment))]
    private bool _isCheckoutValid;

    public OrderCartService Cart => _orderCartService;

    public bool IsCashSelected => string.Equals(SelectedPaymentMethod, PaymentMethods.Cash, StringComparison.OrdinalIgnoreCase);
    public bool IsGCashSelected => string.Equals(SelectedPaymentMethod, PaymentMethods.GCash, StringComparison.OrdinalIgnoreCase);
    public bool IsCreditCardSelected => string.Equals(SelectedPaymentMethod, PaymentMethods.CreditCard, StringComparison.OrdinalIgnoreCase);
    public bool RequiresReferenceNumber => IsGCashSelected || IsCreditCardSelected;
    public bool HasCustomerOrTableError => !string.IsNullOrWhiteSpace(CustomerOrTableError);
    public bool HasAmountTenderedError => !string.IsNullOrWhiteSpace(AmountTenderedError);
    public bool HasReferenceNumberError => !string.IsNullOrWhiteSpace(ReferenceNumberError);
    public bool CanCompletePayment => IsCheckoutValid && IsNotBusy;

    public string ReferenceFieldTitle => IsGCashSelected
        ? "GCash Reference Number (6-20 alphanumeric chars)"
        : "Card Approval / Trace Reference Number (4-20 alphanumeric chars)";

    public decimal SubtotalAmount => _orderCartService.SubtotalAmount;

    public decimal DiscountValue =>
        decimal.TryParse(DiscountAmountText, out var parsed) && parsed >= 0m
            ? Math.Min(parsed, SubtotalAmount)
            : 0m;

    public decimal TotalPayableAmount => Math.Max(0m, SubtotalAmount - DiscountValue);

    public decimal AmountTenderedValue =>
        IsCashSelected
            ? (decimal.TryParse(AmountTenderedText, out var tendered) && tendered >= 0m ? tendered : 0m)
            : TotalPayableAmount;

    public decimal ChangeDueAmount => IsCashSelected
        ? Math.Max(0m, AmountTenderedValue - TotalPayableAmount)
        : 0m;

    public PaymentCheckoutViewModel(
        IOrderTransactionService orderTransactionService,
        OrderCartService orderCartService,
        IDialogService dialogService)
    {
        _orderTransactionService = orderTransactionService;
        _orderCartService = orderCartService;
        _dialogService = dialogService;
        PageTitle = "Multi-Payment Checkout";
    }

    partial void OnCustomerOrTableLabelChanged(string value) => ValidateCheckoutLive();
    partial void OnSelectedPaymentMethodChanged(string value) => ValidateCheckoutLive();
    partial void OnDiscountAmountTextChanged(string value) => ValidateCheckoutLive();
    partial void OnAmountTenderedTextChanged(string value) => ValidateCheckoutLive();
    partial void OnPaymentReferenceNumberChanged(string value) => ValidateCheckoutLive();

    private void ValidateCheckoutLive()
    {
        var summary = ValidationRules.ValidatePointOfSaleCheckout(
            _orderCartService.OrderItems.Count,
            CustomerOrTableLabel,
            SelectedPaymentMethod,
            TotalPayableAmount,
            AmountTenderedText,
            PaymentReferenceNumber);

        CustomerOrTableError = summary.GetError("CustomerOrTableLabel");
        AmountTenderedError = summary.GetError("AmountTendered");
        ReferenceNumberError = summary.GetError("PaymentReferenceNumber");
        IsCheckoutValid = summary.IsValid;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("OrderType", out var orderTypeObj) && orderTypeObj is string typeStr)
            OrderType = typeStr;

        if (query.TryGetValue("CustomerOrTableLabel", out var labelObj) && labelObj is string labelStr)
            CustomerOrTableLabel = labelStr;

        AmountTenderedText = TotalPayableAmount.ToString("0.00", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(SubtotalAmount));
        OnPropertyChanged(nameof(TotalPayableAmount));
        OnPropertyChanged(nameof(ChangeDueAmount));
        ValidateCheckoutLive();
    }

    [RelayCommand]
    private void SelectPaymentMethod(string method)
    {
        ClearError();
        SelectedPaymentMethod = method switch
        {
            PaymentMethods.GCash => PaymentMethods.GCash,
            PaymentMethods.CreditCard => PaymentMethods.CreditCard,
            _ => PaymentMethods.Cash
        };

        if (IsCashSelected && string.IsNullOrWhiteSpace(AmountTenderedText))
        {
            AmountTenderedText = TotalPayableAmount.ToString("0.00", CultureInfo.InvariantCulture);
        }

        ValidateCheckoutLive();
    }

    [RelayCommand]
    private void ApplyQuickCashDenomination(string denominationKey)
    {
        ClearError();
        SelectedPaymentMethod = PaymentMethods.Cash;

        if (string.Equals(denominationKey, "Exact", StringComparison.OrdinalIgnoreCase))
        {
            AmountTenderedText = TotalPayableAmount.ToString("0.00", CultureInfo.InvariantCulture);
            return;
        }

        if (decimal.TryParse(denominationKey, out var billAmount))
        {
            AmountTenderedText = billAmount.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }

    [RelayCommand]
    private async Task CompletePaymentTransactionAsync()
    {
        if (IsBusy) return;
        ClearError();
        ValidateCheckoutLive();

        if (!IsCheckoutValid)
        {
            ErrorMessage = "Please fix the highlighted checkout validation errors.";
            return;
        }

        try
        {
            IsBusy = true;
            OnPropertyChanged(nameof(CanCompletePayment));

            var request = new CheckoutTransactionRequest
            {
                OrderType = OrderType,
                CustomerOrTableLabel = CustomerOrTableLabel?.Trim() ?? string.Empty,
                OrderItems = _orderCartService.OrderItems.ToList(),
                DiscountAmount = DiscountValue,
                PaymentMethod = SelectedPaymentMethod,
                PaymentReferenceNumber = PaymentReferenceNumber?.Trim() ?? string.Empty,
                AmountTendered = IsCashSelected ? AmountTenderedValue : TotalPayableAmount
            };

            var completedOrder = await _orderTransactionService.ProcessCheckoutAsync(request);
            _orderCartService.ClearCart();

            var parameters = new Dictionary<string, object>
            {
                ["CompletedOrder"] = completedOrder
            };

            await Shell.Current.GoToAsync(nameof(ReceiptPreviewPage), parameters);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanCompletePayment));
        }
    }

    [RelayCommand]
    private async Task CancelCheckoutAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
