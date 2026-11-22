using Wegs_Cafe.Models;

namespace Wegs_Cafe.Helpers;

public enum SalesAnalyticsTimeframe
{
    Day,
    Week,
    Month
}

public record SalesChartBucket(
    string Label,
    DateTime BucketStartLocal,
    decimal Revenue,
    int OrderCount);

public record TopSellingProductRank(
    int Rank,
    string ProductName,
    string Category,
    intUnitsSold QuantitySold,
    decimal Revenue,
    double ShareRatio);

public record intUnitsSold(int Value)
{
    public override string ToString() => $"{Value} sold";
}

public record CategoryPerformanceSummary(
    string Category,
    int UnitsSold,
    decimal Revenue,
    double ShareRatio);

public record SalesAnalyticsSnapshot
{
    public SalesAnalyticsTimeframe Timeframe { get; init; }
    public string TimeframeTitle { get; init; } = string.Empty;
    public decimal GrossSalesRevenue { get; init; }
    public decimal EstimatedGrossProfit { get; init; }
    public double ProfitMarginRatio { get; init; }
    public int CompletedOrderCount { get; init; }
    public int VoidedOrderCount { get; init; }
    public decimal AverageOrderTicket { get; init; }
    public int TotalItemsSold { get; init; }
    public decimal CashSalesRevenue { get; init; }
    public decimal GCashSalesRevenue { get; init; }
    public decimal CreditCardSalesRevenue { get; init; }
    public IReadOnlyList<SalesChartBucket> ChartBuckets { get; init; } = Array.Empty<SalesChartBucket>();
    public IReadOnlyList<TopSellingProductRank> TopSellingProducts { get; init; } = Array.Empty<TopSellingProductRank>();
    public IReadOnlyList<CategoryPerformanceSummary> CategoryBreakdown { get; init; } = Array.Empty<CategoryPerformanceSummary>();
    public IReadOnlyList<Order> RecentOrders { get; init; } = Array.Empty<Order>();
    public IReadOnlyList<Order> FilteredOrders { get; init; } = Array.Empty<Order>();
}

public static class SalesAnalyticsCalculator
{
    public static SalesAnalyticsSnapshot ComputeSnapshot(
        IReadOnlyList<Order> allOrders,
        IReadOnlyList<Product> allProducts,
        SalesAnalyticsTimeframe timeframe,
        DateTime? referenceUtc = null)
    {
        var nowLocal = (referenceUtc ?? DateTime.UtcNow).ToLocalTime();
        var (startLocal, endLocal, title) = GetWindowRange(timeframe, nowLocal);

        var parsedOrders = allOrders
            .Select(order => (Order: order, LocalTimestamp: ParseLocalTimestamp(order.CreatedAt)))
            .Where(entry => entry.LocalTimestamp >= startLocal && entry.LocalTimestamp < endLocal)
            .OrderByDescending(entry => entry.LocalTimestamp)
            .ToList();

        var completedOrders = parsedOrders
            .Where(entry => string.Equals(entry.Order.OrderStatus, OrderStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var voidedCount = parsedOrders
            .Count(entry => string.Equals(entry.Order.OrderStatus, OrderStatuses.Voided, StringComparison.OrdinalIgnoreCase));

        var productLookup = allProducts.ToDictionary(product => product.ProductIdentifier, StringComparer.OrdinalIgnoreCase);

        decimal grossRevenue = 0m;
        decimal totalCost = 0m;
        int totalItemsSold = 0;
        decimal cashRevenue = 0m;
        decimal gcashRevenue = 0m;
        decimal cardRevenue = 0m;

        var productAggregation = new Dictionary<string, (string Name, string Category, int Quantity, decimal Revenue)>(StringComparer.OrdinalIgnoreCase);
        var categoryAggregation = new Dictionary<string, (int Units, decimal Revenue)>(StringComparer.OrdinalIgnoreCase);

        foreach (var (order, _) in completedOrders)
        {
            grossRevenue += order.TotalAmount;

            if (string.Equals(order.PaymentMethod, PaymentMethods.Cash, StringComparison.OrdinalIgnoreCase))
                cashRevenue += order.TotalAmount;
            else if (string.Equals(order.PaymentMethod, PaymentMethods.GCash, StringComparison.OrdinalIgnoreCase))
                gcashRevenue += order.TotalAmount;
            else if (string.Equals(order.PaymentMethod, PaymentMethods.CreditCard, StringComparison.OrdinalIgnoreCase))
                cardRevenue += order.TotalAmount;

            foreach (var item in order.OrderItems)
            {
                totalItemsSold += item.Quantity;

                productLookup.TryGetValue(item.ProductIdentifier, out var catalogProduct);
                var unitCost = catalogProduct?.CostPrice ?? Math.Round(item.UnitPrice * 0.35m, 2);
                var categoryName = catalogProduct?.Category ?? ProductCategories.Coffee;
                totalCost += unitCost * item.Quantity;

                var key = string.IsNullOrWhiteSpace(item.ProductIdentifier) ? item.ProductName : item.ProductIdentifier;
                if (productAggregation.TryGetValue(key, out var existingProduct))
                {
                    productAggregation[key] = (
                        existingProduct.Name,
                        existingProduct.Category,
                        existingProduct.Quantity + item.Quantity,
                        existingProduct.Revenue + item.LineSubtotal);
                }
                else
                {
                    productAggregation[key] = (item.ProductName, categoryName, item.Quantity, item.LineSubtotal);
                }

                if (categoryAggregation.TryGetValue(categoryName, out var existingCategory))
                {
                    categoryAggregation[categoryName] = (
                        existingCategory.Units + item.Quantity,
                        existingCategory.Revenue + item.LineSubtotal);
                }
                else
                {
                    categoryAggregation[categoryName] = (item.Quantity, item.LineSubtotal);
                }
            }
        }

        var grossProfit = Math.Max(0m, grossRevenue - totalCost);
        var profitMarginRatio = grossRevenue > 0m ? (double)(grossProfit / grossRevenue) : 0d;
        var averageTicket = completedOrders.Count > 0 ? Math.Round(grossRevenue / completedOrders.Count, 2) : 0m;

        var buckets = BuildChartBuckets(timeframe, startLocal, nowLocal, completedOrders);

        var topProductsRaw = productAggregation.Values
            .OrderByDescending(entry => entry.Revenue)
            .ThenByDescending(entry => entry.Quantity)
            .Take(6)
            .ToList();

        var maxProductRevenue = topProductsRaw.Count > 0 ? Math.Max(1m, topProductsRaw.Max(entry => entry.Revenue)) : 1m;
        var topProducts = topProductsRaw
            .Select((entry, index) => new TopSellingProductRank(
                index + 1,
                entry.Name,
                entry.Category,
                new intUnitsSold(entry.Quantity),
                entry.Revenue,
                (double)(entry.Revenue / maxProductRevenue)))
            .ToList();

        var totalCategoryRevenue = Math.Max(1m, categoryAggregation.Values.Sum(entry => entry.Revenue));
        var categorySummaries = categoryAggregation
            .OrderByDescending(pair => pair.Value.Revenue)
            .Select(pair => new CategoryPerformanceSummary(
                pair.Key,
                pair.Value.Units,
                pair.Value.Revenue,
                (double)(pair.Value.Revenue / totalCategoryRevenue)))
            .ToList();

        return new SalesAnalyticsSnapshot
        {
            Timeframe = timeframe,
            TimeframeTitle = title,
            GrossSalesRevenue = grossRevenue,
            EstimatedGrossProfit = grossProfit,
            ProfitMarginRatio = profitMarginRatio,
            CompletedOrderCount = completedOrders.Count,
            VoidedOrderCount = voidedCount,
            AverageOrderTicket = averageTicket,
            TotalItemsSold = totalItemsSold,
            CashSalesRevenue = cashRevenue,
            GCashSalesRevenue = gcashRevenue,
            CreditCardSalesRevenue = cardRevenue,
            ChartBuckets = buckets,
            TopSellingProducts = topProducts,
            CategoryBreakdown = categorySummaries,
            RecentOrders = parsedOrders.Take(10).Select(entry => entry.Order).ToList(),
            FilteredOrders = parsedOrders.Select(entry => entry.Order).ToList()
        };
    }

    private static (DateTime StartLocal, DateTime EndLocal, string Title) GetWindowRange(
        SalesAnalyticsTimeframe timeframe,
        DateTime nowLocal)
    {
        var today = nowLocal.Date;
        return timeframe switch
        {
            SalesAnalyticsTimeframe.Day => (
                today,
                today.AddDays(1),
                $"Today ({today:MMM dd, yyyy}) — Hourly Sales"),

            SalesAnalyticsTimeframe.Week => (
                today.AddDays(-6),
                today.AddDays(1),
                $"Last 7 Days ({today.AddDays(-6):MMM dd} - {today:MMM dd, yyyy}) — Daily Sales"),

            SalesAnalyticsTimeframe.Month => (
                new DateTime(today.Year, today.Month, 1),
                today.AddDays(1),
                $"{today:MMMM yyyy} — Daily Sales"),

            _ => (today, today.AddDays(1), "Today")
        };
    }

    private static List<SalesChartBucket> BuildChartBuckets(
        SalesAnalyticsTimeframe timeframe,
        DateTime startLocal,
        DateTime nowLocal,
        List<(Order Order, DateTime LocalTimestamp)> completedOrders)
    {
        var buckets = new List<SalesChartBucket>();

        if (timeframe == SalesAnalyticsTimeframe.Day)
        {
            for (var hour = 7; hour <= 21; hour += 2)
            {
                var slotStart = startLocal.AddHours(hour);
                var slotEnd = slotStart.AddHours(2);
                var matching = completedOrders
                    .Where(entry => entry.LocalTimestamp >= slotStart && entry.LocalTimestamp < slotEnd)
                    .ToList();

                var label = slotStart.ToString("htt").ToLowerInvariant();
                buckets.Add(new SalesChartBucket(
                    label,
                    slotStart,
                    matching.Sum(entry => entry.Order.TotalAmount),
                    matching.Count));
            }
        }
        else if (timeframe == SalesAnalyticsTimeframe.Week)
        {
            for (var index = 0; index < 7; index++)
            {
                var dayStart = startLocal.AddDays(index);
                var dayEnd = dayStart.AddDays(1);
                var matching = completedOrders
                    .Where(entry => entry.LocalTimestamp >= dayStart && entry.LocalTimestamp < dayEnd)
                    .ToList();

                buckets.Add(new SalesChartBucket(
                    dayStart.ToString("ddd"),
                    dayStart,
                    matching.Sum(entry => entry.Order.TotalAmount),
                    matching.Count));
            }
        }
        else
        {
            var totalDays = Math.Max(1, (nowLocal.Date - startLocal.Date).Days + 1);
            for (var index = 0; index < totalDays; index++)
            {
                var dayStart = startLocal.AddDays(index);
                var dayEnd = dayStart.AddDays(1);
                var matching = completedOrders
                    .Where(entry => entry.LocalTimestamp >= dayStart && entry.LocalTimestamp < dayEnd)
                    .ToList();

                buckets.Add(new SalesChartBucket(
                    dayStart.Day.ToString(),
                    dayStart,
                    matching.Sum(entry => entry.Order.TotalAmount),
                    matching.Count));
            }
        }

        return buckets;
    }

    public static DateTime ParseLocalTimestamp(string? isoTimestamp)
    {
        var parsedUtc = IsoTimestamp.ParseOrDefault(isoTimestamp);
        return parsedUtc == DateTime.MinValue ? DateTime.Now : parsedUtc.ToLocalTime();
    }
}
