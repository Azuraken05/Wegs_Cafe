using System.Text;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Helpers;

public class ReceiptLine
{
    public string Text { get; init; } = string.Empty;
    public bool Bold { get; init; }
    public bool Large { get; init; }   // double-size (header). Centered for width/2 characters.
}

/// <summary>Builds the receipt text once; used by the preview, the PDF and the ESC/POS printer.</summary>
public static class ReceiptBuilder
{
    public static List<ReceiptLine> Build(Order o, int width, string currency = "₱")
    {
        var lines = new List<ReceiptLine>();
        string Money(decimal v) => $"{currency}{v:N2}";
        void Add(string t = "", bool bold = false, bool large = false) => lines.Add(new ReceiptLine { Text = t, Bold = bold, Large = large });
        string Center(string t, int w) => t.Length >= w ? t[..w] : new string(' ', (w - t.Length) / 2) + t;
        string Pair(string l, string r)
        {
            var space = width - l.Length - r.Length;
            if (space < 1) { l = l[..Math.Max(0, width - r.Length - 1)]; space = Math.Max(1, width - l.Length - r.Length); }
            return l + new string(' ', space) + r;
        }
        var rule = new string('-', width);

        Add(Center("WEG'S CAFE", width / 2), bold: true, large: true);
        Add(Center("Official Receipt", width));
        Add(rule);
        Add($"Receipt #: {o.ReceiptNumber}");
        Add($"Date: {IsoDate.ToLocal(o.CreatedAt):yyyy-MM-dd h:mm tt}");
        Add($"Cashier: {o.CashierName}");
        Add($"Type: {o.OrderType}");
        if (!string.IsNullOrWhiteSpace(o.TableOrCustomer)) Add($"Table/Cust: {o.TableOrCustomer}");
        Add(rule);

        foreach (var i in o.Items)
        {
            Add(Pair($"{i.Qty}x {i.Name}", Money(i.Subtotal)));
            if (i.Qty > 1) Add($"   @ {Money(i.UnitPrice)}");
            if (!string.IsNullOrWhiteSpace(i.Notes)) Add($"   * {i.Notes}");
        }
        Add(rule);
        Add(Pair("Subtotal", Money(o.Subtotal)));
        if (o.DiscountAmount > 0) Add(Pair("Discount", "-" + Money(o.DiscountAmount)));
        Add(Pair("TOTAL", Money(o.TotalAmount)), bold: true);
        Add(rule);
        Add(Pair("Payment", o.PaymentMethod));
        if (!string.IsNullOrWhiteSpace(o.PaymentReference)) Add(Pair("Ref #", o.PaymentReference));
        if (!string.IsNullOrWhiteSpace(o.CardLast4)) Add(Pair("Card", "**** " + o.CardLast4));
        if (o.PaymentMethod == PaymentMethods.Cash)
        {
            Add(Pair("Cash tendered", Money(o.AmountTendered)));
            Add(Pair("Change", Money(o.ChangeDue)));
        }
        Add(rule);
        if (o.Status == OrderStatuses.Voided) { Add(Center("*** VOIDED ***", width), bold: true); Add(rule); }
        Add(Center("Thank you for visiting", width));
        Add(Center("Weg's Cafe! Come again :)", width));
        return lines;
    }

    public static List<ReceiptLine> BuildTest(int width)
    {
        string Center(string t) => t.Length >= width ? t : new string(' ', (width - t.Length) / 2) + t;
        return new()
        {
            new() { Text = "WEG'S CAFE", Bold = true, Large = true },
            new() { Text = Center("Printer test") },
            new() { Text = new string('-', width) },
            new() { Text = "If you can read this," },
            new() { Text = "your printer is working." },
            new() { Text = new string('-', width) }
        };
    }

    public static string ToPlainText(IEnumerable<ReceiptLine> lines)
        => string.Join(Environment.NewLine, lines.Select(l => l.Text));

    /// <summary>Raw ESC/POS bytes for 58mm (32 chars) / 80mm (48 chars) thermal printers.</summary>
    public static byte[] ToEscPos(IEnumerable<ReceiptLine> lines)
    {
        var bytes = new List<byte>();
        bytes.AddRange(new byte[] { 0x1B, 0x40 });                         // ESC @  initialise
        foreach (var l in lines)
        {
            bytes.AddRange(new byte[] { 0x1B, 0x45, (byte)(l.Bold ? 1 : 0) });   // ESC E n  bold
            bytes.AddRange(new byte[] { 0x1D, 0x21, (byte)(l.Large ? 0x11 : 0x00) }); // GS ! n  size
            bytes.AddRange(Encoding.ASCII.GetBytes(Ascii(l.Text)));
            bytes.Add(0x0A);                                               // LF
        }
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00, 0x1D, 0x21, 0x00 }); // reset styles
        bytes.AddRange(new byte[] { 0x1B, 0x64, 0x04 });                   // ESC d 4  feed 4 lines
        bytes.AddRange(new byte[] { 0x1D, 0x56, 0x42, 0x00 });             // GS V B 0  partial cut
        return bytes.ToArray();
    }

    // Thermal printers have no peso sign / accents in the default code page.
    private static string Ascii(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Replace("₱", "PHP "))
            sb.Append(c < 128 ? c : '?');
        return sb.ToString();
    }
}

