using System.Text;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Helpers;

/// <summary>
/// Formats plain-text receipt previews and ESC/POS byte commands for 58mm (32 chars) and 80mm (48 chars) thermal printers.
/// Strictly zero emojis for hardware thermal printer compatibility.
/// </summary>
public static class ReceiptFormatter
{
    public const string StoreTitle = "WEG'S CAFE";
    public const string StoreTagline = "Specialty Coffee & Comfort Kitchen";
    public const string StoreAddress = "Manila, Philippines";
    public const string ReceiptFooter = "Thank you for visiting Weg's Cafe!";

    public static string FormatPlainTextReceipt(Order order, int lineWidth = 32)
    {
        lineWidth = Math.Clamp(lineWidth, 28, 48);
        var builder = new StringBuilder();
        var singleDivider = new string('-', lineWidth);
        var doubleDivider = new string('=', lineWidth);

        builder.AppendLine(CenterText(StoreTitle, lineWidth));
        builder.AppendLine(CenterText(StoreTagline, lineWidth));
        builder.AppendLine(CenterText(StoreAddress, lineWidth));
        builder.AppendLine(doubleDivider);

        var localDate = SalesAnalyticsCalculator.ParseLocalTimestamp(order.CreatedAt);
        builder.AppendLine(TwoColumnLine("Receipt:", order.ReceiptNumber, lineWidth));
        builder.AppendLine(TwoColumnLine("Date:", localDate.ToString("yyyy-MM-dd HH:mm"), lineWidth));
        builder.AppendLine(TwoColumnLine("Cashier:", order.CashierName, lineWidth));
        builder.AppendLine(TwoColumnLine("Service:", order.OrderType, lineWidth));

        if (!string.IsNullOrWhiteSpace(order.CustomerOrTableLabel))
        {
            builder.AppendLine(TwoColumnLine("Customer/Table:", order.CustomerOrTableLabel, lineWidth));
        }

        if (string.Equals(order.OrderStatus, OrderStatuses.Voided, StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine(singleDivider);
            builder.AppendLine(CenterText("*** VOIDED TRANSACTION ***", lineWidth));
        }

        builder.AppendLine(singleDivider);
        builder.AppendLine(TwoColumnLine("ITEM", "AMOUNT", lineWidth));
        builder.AppendLine(singleDivider);

        foreach (var item in order.OrderItems)
        {
            builder.AppendLine(TruncateText(item.ProductName, lineWidth));
            var quantityLine = $"  {item.Quantity} x PHP {item.UnitPrice:N2}";
            var subtotalText = $"PHP {item.LineSubtotal:N2}";
            builder.AppendLine(TwoColumnLine(quantityLine, subtotalText, lineWidth));

            if (!string.IsNullOrWhiteSpace(item.CupSize) &&
                (!string.Equals(item.CupSize, "Regular", StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(item.IceLevel, "Regular Ice", StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(item.SweetnessLevel, "100%", StringComparison.OrdinalIgnoreCase) ||
                 item.AddOns.Count > 0))
            {
                var modText = $"  {item.CupSize}, {item.IceLevel}, {item.SweetnessLevel}";
                if (item.AddOns.Count > 0)
                    modText += $", {string.Join(", ", item.AddOns)}";
                builder.AppendLine(TruncateText(modText, lineWidth));
            }

            if (!string.IsNullOrWhiteSpace(item.PreparationNotes))
            {
                builder.AppendLine(TruncateText($"  Note: {item.PreparationNotes}", lineWidth));
            }
        }

        builder.AppendLine(singleDivider);
        builder.AppendLine(TwoColumnLine("Subtotal:", $"PHP {order.SubtotalAmount:N2}", lineWidth));

        if (order.DiscountAmount > 0)
        {
            builder.AppendLine(TwoColumnLine("Discount:", $"-PHP {order.DiscountAmount:N2}", lineWidth));
        }

        builder.AppendLine(doubleDivider);
        builder.AppendLine(TwoColumnLine("TOTAL:", $"PHP {order.TotalAmount:N2}", lineWidth));
        builder.AppendLine(doubleDivider);
        builder.AppendLine(TwoColumnLine("Payment Method:", order.PaymentMethod, lineWidth));

        if (string.Equals(order.PaymentMethod, PaymentMethods.Cash, StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine(TwoColumnLine("Cash Tendered:", $"PHP {order.AmountTendered:N2}", lineWidth));
            builder.AppendLine(TwoColumnLine("Change Due:", $"PHP {order.ChangeDue:N2}", lineWidth));
        }
        else if (!string.IsNullOrWhiteSpace(order.PaymentReferenceNumber))
        {
            builder.AppendLine(TwoColumnLine("Reference No:", order.PaymentReferenceNumber, lineWidth));
        }

        builder.AppendLine(singleDivider);
        builder.AppendLine(CenterText(ReceiptFooter, lineWidth));
        builder.AppendLine(CenterText("Official POS Transaction Record", lineWidth));

        return builder.ToString();
    }

    public static byte[] BuildEscPosPayload(Order order, int lineWidth = 32)
    {
        var stream = new MemoryStream();
        // ESC @ Initialize printer
        stream.Write(new byte[] { 0x1B, 0x40 });
        // Align left
        stream.Write(new byte[] { 0x1B, 0x61, 0x00 });

        var receiptText = FormatPlainTextReceipt(order, lineWidth);
        var asciiBytes = Encoding.ASCII.GetBytes(receiptText.Replace("\r\n", "\n"));
        stream.Write(asciiBytes);

        // Feed 4 lines & partial cut
        stream.Write(new byte[] { 0x0A, 0x0A, 0x0A, 0x0A });
        stream.Write(new byte[] { 0x1D, 0x56, 0x42, 0x00 });
        return stream.ToArray();
    }

    private static string CenterText(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Length >= width) return text[..width];
        var leftPadding = (width - text.Length) / 2;
        return new string(' ', leftPadding) + text;
    }

    private static string TwoColumnLine(string left, string right, int width)
    {
        left ??= string.Empty;
        right ??= string.Empty;
        var availableLeft = Math.Max(4, width - right.Length - 1);
        if (left.Length > availableLeft)
        {
            left = left[..availableLeft];
        }
        var spaces = Math.Max(1, width - left.Length - right.Length);
        return left + new string(' ', spaces) + right;
    }

    private static string TruncateText(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= width ? text : text[..width];
    }
}
