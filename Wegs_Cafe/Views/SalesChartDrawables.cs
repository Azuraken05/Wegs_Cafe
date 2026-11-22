using Microsoft.Maui.Graphics;
using Wegs_Cafe.Helpers;

namespace Wegs_Cafe.Views;

/// <summary>
/// Renders the Sales Revenue and Order Count Bar + Trend Line Chart for Day, Week, and Month timeframes.
/// </summary>
public class SalesTrendBarChartDrawable : IDrawable
{
    private readonly IReadOnlyList<SalesChartBucket> _buckets;

    private static readonly Color PrimaryBlackColor = Color.FromArgb("#0A0A0A");
    private static readonly Color OffWhiteColor = Color.FromArgb("#F7F6F2");
    private static readonly Color GridLineColor = Color.FromArgb("#E2E2E0");
    private static readonly Color MutedTextColor = Color.FromArgb("#6E6E73");
    private static readonly Color AccentGreenColor = Color.FromArgb("#16A34A");

    public SalesTrendBarChartDrawable(IReadOnlyList<SalesChartBucket> buckets)
    {
        _buckets = buckets ?? Array.Empty<SalesChartBucket>();
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();

        var width = dirtyRect.Width;
        var height = dirtyRect.Height;
        if (width <= 40 || height <= 40)
        {
            canvas.RestoreState();
            return;
        }

        const float leftMargin = 58f;
        const float rightMargin = 16f;
        const float topMargin = 22f;
        const float bottomMargin = 34f;

        var plotWidth = Math.Max(10f, width - leftMargin - rightMargin);
        var plotHeight = Math.Max(10f, height - topMargin - bottomMargin);

        var maxRevenue = _buckets.Count > 0
            ? Math.Max(500m, _buckets.Max(bucket => bucket.Revenue) * 1.15m)
            : 1000m;

        // Horizontal reference grid lines + Y-axis labels
        const int gridSteps = 4;
        canvas.StrokeSize = 1f;
        canvas.FontSize = 10f;
        canvas.FontColor = MutedTextColor;

        for (var step = 0; step <= gridSteps; step++)
        {
            var ratio = (float)step / gridSteps;
            var y = topMargin + plotHeight - (ratio * plotHeight);
            var value = (decimal)ratio * maxRevenue;

            canvas.StrokeColor = GridLineColor;
            canvas.DrawLine(leftMargin, y, leftMargin + plotWidth, y);

            var label = value >= 1000m ? $"₱{value / 1000m:0.#}k" : $"₱{value:0}";
            canvas.DrawString(
                label,
                2f,
                y - 8f,
                leftMargin - 8f,
                16f,
                HorizontalAlignment.Right,
                VerticalAlignment.Center);
        }

        if (_buckets.Count == 0)
        {
            canvas.FontSize = 12f;
            canvas.FontColor = MutedTextColor;
            canvas.DrawString(
                "No completed sales transactions recorded in this timeframe.",
                leftMargin,
                topMargin,
                plotWidth,
                plotHeight,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
            canvas.RestoreState();
            return;
        }

        var count = _buckets.Count;
        var slotWidth = plotWidth / count;
        var barWidth = Math.Clamp(slotWidth * 0.56f, 8f, 38f);
        var points = new List<PointF>(count);

        for (var index = 0; index < count; index++)
        {
            var bucket = _buckets[index];
            var ratio = maxRevenue > 0m ? (float)(bucket.Revenue / maxRevenue) : 0f;
            var barHeight = Math.Max(ratio > 0f ? 6f : 2f, ratio * plotHeight);

            var centerX = leftMargin + (index * slotWidth) + (slotWidth / 2f);
            var barLeft = centerX - (barWidth / 2f);
            var barTop = topMargin + plotHeight - barHeight;

            // Track background bar
            canvas.FillColor = OffWhiteColor;
            canvas.FillRoundedRectangle(barLeft, topMargin, barWidth, plotHeight, 6f);

            // Actual revenue bar
            canvas.FillColor = bucket.Revenue > 0m ? PrimaryBlackColor : GridLineColor;
            canvas.FillRoundedRectangle(barLeft, barTop, barWidth, barHeight, 6f);

            points.Add(new PointF(centerX, barTop));

            // Value callout on top of non-zero bars (if enough horizontal space)
            if (bucket.Revenue > 0m && count <= 12)
            {
                canvas.FontSize = 9.5f;
                canvas.FontColor = PrimaryBlackColor;
                var valueLabel = bucket.Revenue >= 1000m
                    ? $"₱{bucket.Revenue / 1000m:0.0}k"
                    : $"₱{bucket.Revenue:0}";

                canvas.DrawString(
                    valueLabel,
                    centerX - 28f,
                    Math.Max(2f, barTop - 16f),
                    56f,
                    14f,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
            }

            // X-axis label (skip every second label when showing > 16 days in month view)
            if (count <= 16 || index % 2 == 0 || index == count - 1)
            {
                canvas.FontSize = 10f;
                canvas.FontColor = MutedTextColor;
                canvas.DrawString(
                    bucket.Label,
                    centerX - 24f,
                    topMargin + plotHeight + 6f,
                    48f,
                    20f,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
            }
        }

        // Smooth trend line overlay
        if (points.Count > 1)
        {
            canvas.StrokeColor = AccentGreenColor;
            canvas.StrokeSize = 2f;
            for (var index = 0; index < points.Count - 1; index++)
            {
                canvas.DrawLine(points[index], points[index + 1]);
            }

            foreach (var point in points)
            {
                canvas.FillColor = Colors.White;
                canvas.FillCircle(point.X, point.Y, 4f);
                canvas.StrokeColor = AccentGreenColor;
                canvas.StrokeSize = 2f;
                canvas.DrawCircle(point.X, point.Y, 4f);
            }
        }

        canvas.RestoreState();
    }
}

/// <summary>
/// Renders a Horizontal Proportional Bar and Legend for Cash, GCash, and Credit Card revenue split.
/// </summary>
public class PaymentMethodBreakdownDrawable : IDrawable
{
    private readonly decimal _cashAmount;
    private readonly decimal _gcashAmount;
    private readonly decimal _cardAmount;

    private static readonly Color CashColor = Color.FromArgb("#0A0A0A");
    private static readonly Color GCashColor = Color.FromArgb("#16A34A");
    private static readonly Color CardColor = Color.FromArgb("#D97706");
    private static readonly Color EmptyTrackColor = Color.FromArgb("#E2E2E0");
    private static readonly Color MutedTextColor = Color.FromArgb("#6E6E73");

    public PaymentMethodBreakdownDrawable(decimal cashAmount, decimal gcashAmount, decimal cardAmount)
    {
        _cashAmount = cashAmount;
        _gcashAmount = gcashAmount;
        _cardAmount = cardAmount;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();

        var width = dirtyRect.Width;
        var height = dirtyRect.Height;
        if (width <= 20 || height <= 20)
        {
            canvas.RestoreState();
            return;
        }

        var total = _cashAmount + _gcashAmount + _cardAmount;
        const float barHeight = 22f;
        const float barTop = 8f;

        if (total <= 0m)
        {
            canvas.FillColor = EmptyTrackColor;
            canvas.FillRoundedRectangle(0, barTop, width, barHeight, 11f);
            canvas.FontSize = 11f;
            canvas.FontColor = MutedTextColor;
            canvas.DrawString(
                "No payments recorded in this period",
                0,
                barTop + barHeight + 8f,
                width,
                22f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
            canvas.RestoreState();
            return;
        }

        var cashWidth = (float)(_cashAmount / total) * width;
        var gcashWidth = (float)(_gcashAmount / total) * width;
        var cardWidth = Math.Max(0f, width - cashWidth - gcashWidth);

        var currentX = 0f;
        if (cashWidth > 0f)
        {
            canvas.FillColor = CashColor;
            canvas.FillRectangle(currentX, barTop, cashWidth, barHeight);
            currentX += cashWidth;
        }

        if (gcashWidth > 0f)
        {
            canvas.FillColor = GCashColor;
            canvas.FillRectangle(currentX, barTop, gcashWidth, barHeight);
            currentX += gcashWidth;
        }

        if (cardWidth > 0f)
        {
            canvas.FillColor = CardColor;
            canvas.FillRectangle(currentX, barTop, cardWidth, barHeight);
        }

        // Draw percentage labels below
        var legendTop = barTop + barHeight + 10f;
        var thirdWidth = width / 3f;

        DrawLegendEntry(canvas, 0f, legendTop, thirdWidth, CashColor, $"Cash ({(_cashAmount / total) * 100:0}%");
        DrawLegendEntry(canvas, thirdWidth, legendTop, thirdWidth, GCashColor, $"GCash ({(_gcashAmount / total) * 100:0}%)");
        DrawLegendEntry(canvas, thirdWidth * 2f, legendTop, thirdWidth, CardColor, $"Card ({(_cardAmount / total) * 100:0}%)");

        canvas.RestoreState();
    }

    private static void DrawLegendEntry(ICanvas canvas, float x, float y, float width, Color dotColor, string text)
    {
        if (!text.EndsWith(")")) text += ")";
        canvas.FillColor = dotColor;
        canvas.FillCircle(x + 8f, y + 8f, 5f);

        canvas.FontSize = 11f;
        canvas.FontColor = Color.FromArgb("#0A0A0A");
        canvas.DrawString(text, x + 18f, y, width - 20f, 18f, HorizontalAlignment.Left, VerticalAlignment.Center);
    }
}
