using System.Text;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
#if ANDROID
using AG = Android.Graphics;
using Android.Graphics.Pdf;
#endif

namespace Wegs_Cafe.Services;

/// <summary>
/// Generates shareable receipt documents (PDF on Android via PdfDocument, formatted text file fallback on desktop).
/// </summary>
public class ReceiptDocumentService : IReceiptDocumentService
{
    public async Task<string?> GenerateReceiptPdfAsync(Order order)
    {
        var receiptLines = ReceiptFormatter.FormatPlainTextReceipt(order, 32)
            .Replace("\r\n", "\n")
            .Split('\n');

#if ANDROID
        var fileName = $"{order.ReceiptNumber}.pdf";
        var filePath = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);

        await Task.Run(() =>
        {
            using var document = new PdfDocument();
            var lineHeight = 16;
            var padding = 24;
            var pageWidth = 300;
            var pageHeight = Math.Max(420, (receiptLines.Length * lineHeight) + (padding * 2));

            var pageInfo = new PdfDocument.PageInfo.Builder(pageWidth, pageHeight, 1).Create();
            var page = document.StartPage(pageInfo);
            var canvas = page?.Canvas;
            if (canvas == null) return;

            using var backgroundPaint = new AG.Paint { Color = AG.Color.White };
            canvas.DrawRect(0, 0, pageWidth, pageHeight, backgroundPaint);

            using var textPaint = new AG.Paint
            {
                Color = AG.Color.Black,
                TextSize = 11f,
                AntiAlias = true
            };
            textPaint.SetTypeface(AG.Typeface.Monospace);

            var currentY = (float)padding + 12f;
            foreach (var line in receiptLines)
            {
                canvas.DrawText(line, padding, currentY, textPaint);
                currentY += lineHeight;
            }

            document.FinishPage(page);
            using var stream = File.Create(filePath);
            document.WriteTo(stream);
        });

        return filePath;
#else
        var fileName = $"{order.ReceiptNumber}.txt";
        var filePath = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
        var formattedText = ReceiptFormatter.FormatPlainTextReceipt(order, 32);
        await File.WriteAllTextAsync(filePath, formattedText, Encoding.UTF8);
        return filePath;
#endif
    }
}
