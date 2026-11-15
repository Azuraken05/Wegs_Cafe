using System.Globalization;
using Microsoft.Maui.Graphics;
using Wegs_Cafe.Helpers;

namespace Wegs_Cafe.Views;

/// <summary>
/// High-contrast monochrome bar + trend line chart for Sales Overview on the Dashboard.
/// Uses native MAUI GraphicsView (IDrawable) so it renders reliably across Android &amp; Windows without native Skia handler issues.
/// </summary>
public class SalesBarChartDrawable : IDrawable
{
    public IReadOnlyList<SalesBucket> Buckets { get; set; } = Array.Empty<SalesBucket>();
    public bool ShowOrderLine { get; set; } = true;

    private static readonly Color Black = Color.FromArgb("#0A0A0A");
    private static readonly Color Cream = Color.FromArgb("#F7F6F2");
    private static readonly Color BorderGray = Color.FromArgb("#E2E2E0");
    private static readonly Color Muted = Color.FromArgb("#6E6E73");
    private static readonly Color AccentGreen = Color.FromArgb("#16A34A");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        float w = dirtyRect.Width;
        float h = dirtyRect.Height;
        if (w <= 20 || h <= 20)
        {
            canvas.RestoreState();
            return;
        }

        float leftPad = 58f;
        float rightPad = 14f;
        float topPad = 18f;
        float bottomPad = 30f;

        float plotW = Math.Max(10f, w - leftPad - rightPad);
        float plotH = Math.Max(10f, h - topPad - bottomPad);
        float plotBottom = topPad + plotH;

        var buckets = Buckets ?? Array.Empty<SalesBucket>();
        decimal maxVal = buckets.Count > 0 ? buckets.Max(b => b.Total) : 0m;
        float max = maxVal <= 0 ? 1000f : (float)NiceCeiling(maxVal);

        // Horizontal grid lines + Y-axis labels (4 steps)
        const int steps = 4;
        canvas.FontSize = 10f;
        canvas.FontColor = Muted;
        canvas.StrokeSize = 1f;

        for (int i = 0; i <= steps; i++)
        {
            float ratio = (float)i / steps;
            float y = plotBottom - ratio * plotH;

            canvas.StrokeColor = i == 0 ? Black : BorderGray;
            canvas.DrawLine(leftPad, y, leftPad + plotW, y);

            float val = max * ratio;
            string label = val >= 1000f
                ? $"₱{val / 1000f:0.#}k"
                : $"₱{val:0}";
            canvas.DrawString(label, 0, y - 8f, leftPad - 8f, 16f, HorizontalAlignment.Right, VerticalAlignment.Center);
        }

        if (buckets.Count == 0)
        {
            canvas.RestoreState();
            return;
        }

        int n = buckets.Count;
        float slotW = plotW / n;
        float barW = Math.Clamp(slotW * 0.62f, 6f, 34f);

        // Determine label step so X labels never overlap
        int labelEvery = n <= 7 ? 1 : n <= 16 ? 2 : 3;

        var topPoints = new List<PointF>(n);

        for (int i = 0; i < n; i++)
        {
            var b = buckets[i];
            float val = (float)b.Total;
            float ratio = Math.Clamp(val / max, 0f, 1f);
            float barH = ratio * plotH;
            float cx = leftPad + (i + 0.5f) * slotW;
            float bx = cx - barW / 2f;
            float by = plotBottom - barH;

            // Subtle background track for empty/partial slots on weekly view
            if (n <= 12)
            {
                canvas.FillColor = Cream;
                canvas.FillRoundedRectangle(bx, topPad, barW, plotH, 6f);
            }

            if (barH >= 2f)
            {
                canvas.FillColor = Black;
                canvas.FillRoundedRectangle(bx, by, barW, barH, Math.Min(6f, barH / 2f));

                // Value label above non-zero bars when there is room
                if (n <= 12 && val > 0)
                {
                    canvas.FontSize = 9.5f;
                    canvas.FontColor = Black;
                    string vText = val >= 1000f ? $"₱{val / 1000f:0.#}k" : $"₱{val:0}";
                    canvas.DrawString(vText, cx - 28f, Math.Max(2f, by - 15f), 56f, 14f, HorizontalAlignment.Center, VerticalAlignment.Bottom);
                }
            }

            topPoints.Add(new PointF(cx, by));

            // X-axis label
            if (i % labelEvery == 0 || i == n - 1)
            {
                canvas.FontSize = 10f;
                canvas.FontColor = Muted;
                canvas.DrawString(b.Label, cx - 24f, plotBottom + 6f, 48f, 18f, HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }

        // Draw subtle connecting trend line across non-zero points if there are sales
        if (ShowOrderLine && maxVal > 0 && topPoints.Count > 1)
        {
            canvas.StrokeColor = AccentGreen;
            canvas.StrokeSize = 2f;
            for (int i = 0; i < topPoints.Count - 1; i++)
            {
                if (buckets[i].Total > 0 || buckets[i + 1].Total > 0)
                {
                    canvas.DrawLine(topPoints[i], topPoints[i + 1]);
                }
            }
            foreach (var (pt, idx) in topPoints.Select((p, idx) => (p, idx)))
            {
                if (buckets[idx].Total > 0)
                {
                    canvas.FillColor = AccentGreen;
                    canvas.FillCircle(pt.X, pt.Y, 3.5f);
                }
            }
        }

        canvas.RestoreState();
    }

    private static decimal NiceCeiling(decimal value)
    {
        if (value <= 100m) return 100m;
        double d = (double)value;
        double exp = Math.Floor(Math.Log10(d));
        double f = d / Math.Pow(10, exp);
        double nf = f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10;
        return (decimal)(nf * Math.Pow(10, exp));
    }
}

/// <summary>
/// Horizontal segmented bar showing Payment Method distribution (Cash vs GCash vs Card)
/// in the Weg's Cafe monochrome + semantic accent palette.
/// </summary>
public class PaymentBreakdownDrawable : IDrawable
{
    public double CashRatio { get; set; }
    public double GCashRatio { get; set; }
    public double CardRatio { get; set; }

    private static readonly Color Black = Color.FromArgb("#0A0A0A");
    private static readonly Color DarkGray = Color.FromArgb("#6E6E73");
    private static readonly Color Green = Color.FromArgb("#16A34A");
    private static readonly Color EmptyTrack = Color.FromArgb("#E2E2E0");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        float w = dirtyRect.Width;
        float h = dirtyRect.Height;

        canvas.FillColor = EmptyTrack;
        canvas.FillRoundedRectangle(0, 0, w, h, h / 2f);

        double total = CashRatio + GCashRatio + CardRatio;
        if (total <= 0.0001)
        {
            canvas.RestoreState();
            return;
        }

        float x = 0f;
        float cashW = (float)(CashRatio / total) * w;
        float gcashW = (float)(GCashRatio / total) * w;
        float cardW = Math.Max(0f, w - cashW - gcashW);

        if (cashW > 0.5f)
        {
            canvas.FillColor = Black;
            canvas.FillRoundedRectangle(x, 0, cashW, h, 4f);
            x += cashW;
        }
        if (gcashW > 0.5f)
        {
            canvas.FillColor = Green;
            canvas.FillRoundedRectangle(x, 0, gcashW, h, 4f);
            x += gcashW;
        }
        if (cardW > 0.5f)
        {
            canvas.FillColor = DarkGray;
            canvas.FillRoundedRectangle(x, 0, cardW, h, 4f);
        }

        canvas.RestoreState();
    }
}
