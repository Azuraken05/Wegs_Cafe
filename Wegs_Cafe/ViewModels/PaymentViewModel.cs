using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wegs_Cafe.Models;
using Wegs_Cafe.Services;

namespace Wegs_Cafe.ViewModels;

public partial class PaymentViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IOrderService _orders;
    private readonly SessionService _session;
    private string _orderType = OrderTypes.DineIn;
    private string _table = string.Empty;

    public CartService Cart { get; }
    public IReadOnlyList<string> Methods { get; } = new[] { PaymentMethods.Cash, PaymentMethods.GCash, PaymentMethods.CreditCard };

    [ObservableProperty] private string _method = PaymentMethods.Cash;
    [ObservableProperty] private string _tenderedText = string.Empty;
    [ObservableProperty] private string _reference = string.Empty;
    [ObservableProperty] private string _cardLast4 = string.Empty;
    [ObservableProperty] private string _discountText = string.Empty;

    public bool ShowDiscount => _session.CanManageProducts;      // discounts: manager/owner only
    public bool IsCash => Method == PaymentMethods.Cash;
    public bool IsGCash => Method == PaymentMethods.GCash;
    public bool IsCard => Method == PaymentMethods.CreditCard;

    public decimal Subtotal => Cart.Subtotal;
    public decimal Discount => Math.Clamp(Parse(DiscountText), 0, Cart.Subtotal);
    public decimal Total => Cart.Subtotal - Discount;
    public decimal Tendered => Parse(TenderedText);
    public decimal ChangeDue => IsCash ? Math.Max(0, Tendered - Total) : 0;
    public bool CashShort => IsCash && Tendered < Total;
    public string OrderSummary => $"{_orderType}{(string.IsNullOrWhiteSpace(_table) ? "" : " · " + _table)}";

    public PaymentViewModel(IOrderService orders, CartService cart, SessionService session)
    {
        _orders = orders;
        _session = session;
        Cart = cart;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("order_type", out var t) && t is string ts) _orderType = ts;
        if (query.TryGetValue("table", out var tb) && tb is string tbs) _table = tbs;
        OnPropertyChanged(nameof(OrderSummary));
        RefreshTotals();
    }

    partial void OnMethodChanged(string value)
    {
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(IsCash)); OnPropertyChanged(nameof(IsGCash)); OnPropertyChanged(nameof(IsCard));
        RefreshTotals();
    }
    partial void OnTenderedTextChanged(string value) => RefreshTotals();
    partial void OnDiscountTextChanged(string value) => RefreshTotals();

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(Subtotal)); OnPropertyChanged(nameof(Discount)); OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(Tendered)); OnPropertyChanged(nameof(ChangeDue)); OnPropertyChanged(nameof(CashShort));
    }

    [RelayCommand] private void SelectMethod(string method) => Method = method;

    [RelayCommand]
    private void QuickCash(string value)
        => TenderedText = value == "exact" ? Total.ToString("0.##", CultureInfo.InvariantCulture) : value;

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (IsBusy) return;
        ErrorMessage = string.Empty;
        try
        {
            IsBusy = true;
            var request = new CheckoutRequest
            {
                OrderType = _orderType,
                TableOrCustomer = _table,
                Items = Cart.Items.ToList(),
                DiscountAmount = Discount,
                PaymentMethod = Method,
                PaymentReference = Reference,
                CardLast4 = CardLast4,
                AmountTendered = Tendered
            };
            var order = await _orders.CheckoutAsync(request);
            Cart.Clear();
            await Shell.Current.GoToAsync("..");
            await Shell.Current.GoToAsync("receipt", new Dictionary<string, object> { ["order"] = order });
        }
        catch (Exception ex) when (ex is ValidationException or UnauthorizedAccessException) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private Task CancelAsync() => Shell.Current.GoToAsync("..");

    private static decimal Parse(string s)
        => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) || decimal.TryParse(s, out v) ? v : 0;
}
