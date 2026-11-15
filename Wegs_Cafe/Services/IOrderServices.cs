using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

public class CheckoutRequest
{
    public string OrderType { get; set; } = OrderTypes.DineIn;
    public string TableOrCustomer { get; set; } = string.Empty;
    public List<CartItem> Items { get; set; } = new();
    public decimal DiscountAmount { get; set; }
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;
    public string PaymentReference { get; set; } = string.Empty;
    public string CardLast4 { get; set; } = string.Empty;
    public decimal AmountTendered { get; set; }
}

public interface IOrderService
{
    /// <summary>Atomic: validates stock + payment, saves the order, deducts stock, writes inventory + audit logs.</summary>
    Task<Order> CheckoutAsync(CheckoutRequest request);
    /// <summary>Owner/manager: every order. Staff: only their own.</summary>
    Task<IReadOnlyList<Order>> GetOrdersAsync();
    /// <summary>Needs an owner/manager PIN. Restores stock and records VOID_ORDER.</summary>
    Task<Order> VoidOrderAsync(string orderId, string authorizerPin);
}

public interface IShiftService
{
    Task<Shift?> GetOpenShiftAsync();
    Task<Shift> OpenShiftAsync(decimal openingCash);
    /// <summary>X reading: running totals, does not close the shift.</summary>
    Task<ShiftReading> GetReadingAsync(Shift shift);
    /// <summary>Z reading: closes the shift, compares counted cash to expected, flags discrepancies in the audit log.</summary>
    Task<Shift> CloseShiftAsync(decimal actualCash);
}

public record PrinterDevice(string Name, string Address);

public interface IReceiptPrinterService
{
    bool IsSupported { get; }
    PrinterDevice? SelectedPrinter { get; }
    /// <summary>58 or 80.</summary>
    int PaperWidthMm { get; set; }
    int CharsPerLine { get; }
    Task<IReadOnlyList<PrinterDevice>> GetPairedPrintersAsync();
    void SelectPrinter(PrinterDevice? printer);
    Task PrintAsync(Order order);
    Task PrintTestAsync();
}

public interface IReceiptDocumentService
{
    /// <summary>Creates a PDF and returns its path, or null when PDF creation isn't supported on this platform.</summary>
    Task<string?> CreatePdfAsync(Order order);
}

