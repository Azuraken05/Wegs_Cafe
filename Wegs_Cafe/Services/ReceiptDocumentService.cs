using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
#if ANDROID
using Android.Graphics.Pdf;
using AG = Android.Graphics;
#endif

namespace Wegs_Cafe.Services;

/// <summary>PDF fallback so receipts can be tested/shared without a thermal printer.</summary>
public class ReceiptDocumentService : IReceiptDocumentService
{
    private readonly IReceiptPrinterService _printer;
    public ReceiptDocumentService(IReceiptPrinterService printer) => _printer = printer;

#if ANDROID
    public Task<string?> CreatePdfAsync(Order order)
    {
        var lines = ReceiptBuilder.Build(order, _printer.CharsPerLine);
        const float textSize = 10f, lineHeight = 14f, margin = 20f;
        var pageWidth = (int)(_printer.CharsPerLine * textSize * 0.62f + margin * 2);
        var pageHeight = (int)(lines.Count * lineHeight + margin * 2);

        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, $"{order.ReceiptNumber}.pdf");
        var doc = new PdfDocument();
        try
        {
            var page = doc.StartPage(new PdfDocument.PageInfo.Builder(pageWidth, pageHeight, 1).Create());
            using var normal = new AG.Paint { TextSize = textSize, AntiAlias = true };
            normal.SetTypeface(AG.Typeface.Monospace);
            using var bold = new AG.Paint { TextSize = textSize, AntiAlias = true, FakeBoldText = true };
            bold.SetTypeface(AG.Typeface.Monospace);
            using var large = new AG.Paint { TextSize = textSize * 1.8f, AntiAlias = true, FakeBoldText = true };
            large.SetTypeface(AG.Typeface.Monospace);

            var y = margin + textSize;
            foreach (var l in lines)
            {
                var paint = l.Large ? large : l.Bold ? bold : normal;
                page.Canvas.DrawText(l.Text, margin, y, paint);
                y += lineHeight;
            }
            doc.FinishPage(page);
            using var fs = File.Create(path);
            doc.WriteTo(fs);
        }
        finally { doc.Close(); }
        return Task.FromResult<string?>(path);
    }
#else
    public Task<string?> CreatePdfAsync(Order order) => Task.FromResult<string?>(null);
#endif
}
