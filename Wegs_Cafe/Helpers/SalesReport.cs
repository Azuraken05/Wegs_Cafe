using System.Globalization;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Helpers;

public enum ReportPeriod { Day, Week, Month }

public class SalesBucket
{
    public string Label { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Orders { get; set; }
}

public class TopProductStat
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public double BarRatio { get; set; } // 0.0 to 1.0 for visual progress bar
}

public class CategoryStat
{
    public string Category { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public double Percentage { get; set; } // 0 to 100
    public double BarRatio { get; set; }   // 0.0 to 1.0
    public string SummaryText => $"{Percentage:0.#}% · {QuantitySold} sold";
}

public class SalesSummary
{
    public decimal GrossSales { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal EstimatedProfit => GrossSales - EstimatedCost;
    public decimal ProfitMarginPercent => GrossSales <= 0 ? 0 : (EstimatedProfit / GrossSales) * 100m;
    public int TotalOrders { get; set; }
    public int VoidedOrders { get; set; }
    public int TotalItemsSold { get; set; }
    public decimal AverageOrderValue => TotalOrders == 0 ? 0 : GrossSales / TotalOrders;

    public decimal Cash { get; set; }
    public decimal GCash { get; set; }
    public decimal Card { get; set; }
    public int CashCount { get; set; }
    public int GCashCount { get; set; }
    public int CardCount { get; set; }

    public double CashRatio => GrossSales <= 0 ? 0 : (double)(Cash / GrossSales);
    public double GCashRatio => GrossSales <= 0 ? 0 : (double)(GCash / GrossSales);
    public double CardRatio => GrossSales <= 0 ? 0 : (double)(Card / GrossSales);

    public List<SalesBucket> Buckets { get; set; } = new();
    public List<TopProductStat> TopProducts { get; set; } = new();
    public List<CategoryStat> CategoryBreakdown { get; set; } = new();
    public List<Order> RecentOrders { get; set; } = new();
}

/// <summary>Pure calculation: turns orders and products into KPI numbers, chart buckets, and analytics.</summary>
public static class SalesReport
{
    public static SalesSummary Build(IEnumerable<Order> orders, IEnumerable<Product> products, ReportPeriod period, DateTime nowLocal)
    {
        DateTime start, end;   // [start, end)
        var today = nowLocal.Date;
        switch (period)
        {
            case ReportPeriod.Day: start = today; end = today.AddDays(1); break;
            case ReportPeriod.Week:
                var offset = ((int)today.DayOfWeek + 6) % 7;      // Monday = 0
                start = today.AddDays(-offset); end = start.AddDays(7); break;
            default:
                start = new DateTime(today.Year, today.Month, 1); end = start.AddMonths(1); break;
        }

        var allInRange = orders
            .Select(o => (order: o, local: IsoDate.ToLocal(o.CreatedAt)))
            .Where(x => x.local.HasValue && x.local.Value >= start && x.local.Value < end)
            .Select(x => (x.order, local: x.local!.Value))
            .ToList();

        var inRange = allInRange
            .Where(x => x.order.Status == OrderStatuses.Completed)
            .ToList();

        var productLookup = products.ToDictionary(p => p.ProductId, p => p);

        decimal totalCost = 0m;
        int totalItemsSold = 0;
        var productAgg = new Dictionary<string, (string name, int qty, decimal rev)>();
        var categoryAgg = new Dictionary<string, (int qty, decimal rev)>();

        foreach (var cat in ProductCategories.All)
            categoryAgg[cat] = (0, 0m);

        foreach (var (order, _) in inRange)
        {
            foreach (var item in order.Items)
            {
                totalItemsSold += item.Qty;
                if (productLookup.TryGetValue(item.ProductId, out var prod))
                {
                    totalCost += prod.CostPrice * item.Qty;
                    var cat = string.IsNullOrWhiteSpace(prod.Category) ? "Other" : prod.Category;
                    var curCat = categoryAgg.GetValueOrDefault(cat);
                    categoryAgg[cat] = (curCat.qty + item.Qty, curCat.rev + item.Subtotal);
                }

                var key = string.IsNullOrEmpty(item.ProductId) ? item.Name : item.ProductId;
                if (!productAgg.TryGetValue(key, out var curProd))
                    curProd = (item.Name, 0, 0m);
                productAgg[key] = (item.Name, curProd.qty + item.Qty, curProd.rev + item.Subtotal);
            }
        }

        var gross = inRange.Sum(x => x.order.TotalAmount);
        var summary = new SalesSummary
        {
            GrossSales = gross,
            TotalDiscount = inRange.Sum(x => x.order.DiscountAmount),
            EstimatedCost = totalCost,
            TotalOrders = inRange.Count,
            VoidedOrders = allInRange.Count(x => x.order.Status == OrderStatuses.Voided),
            TotalItemsSold = totalItemsSold,
            Cash = inRange.Where(x => x.order.PaymentMethod == PaymentMethods.Cash).Sum(x => x.order.TotalAmount),
            GCash = inRange.Where(x => x.order.PaymentMethod == PaymentMethods.GCash).Sum(x => x.order.TotalAmount),
            Card = inRange.Where(x => x.order.PaymentMethod == PaymentMethods.CreditCard).Sum(x => x.order.TotalAmount),
            CashCount = inRange.Count(x => x.order.PaymentMethod == PaymentMethods.Cash),
            GCashCount = inRange.Count(x => x.order.PaymentMethod == PaymentMethods.GCash),
            CardCount = inRange.Count(x => x.order.PaymentMethod == PaymentMethods.CreditCard),
            RecentOrders = allInRange.OrderByDescending(x => x.local).Take(6).Select(x => x.order).ToList()
        };

        // Top products
        var topList = productAgg
            .Select(kv => new TopProductStat
            {
                ProductId = kv.Key,
                Name = kv.Value.name,
                QuantitySold = kv.Value.qty,
                Revenue = kv.Value.rev
            })
            .OrderByDescending(p => p.Revenue)
            .ThenByDescending(p => p.QuantitySold)
            .Take(5)
            .ToList();

        var maxProdRev = topList.Count > 0 ? topList.Max(p => p.Revenue) : 0m;
        foreach (var tp in topList)
            tp.BarRatio = maxProdRev > 0 ? (double)(tp.Revenue / maxProdRev) : 0;
        summary.TopProducts = topList;

        // Category breakdown
        var totalCatRev = categoryAgg.Values.Sum(v => v.rev);
        summary.CategoryBreakdown = categoryAgg
            .Where(kv => kv.Value.qty > 0 || ProductCategories.All.Contains(kv.Key))
            .Select(kv =>
            {
                var ratio = totalCatRev > 0 ? (double)(kv.Value.rev / totalCatRev) : 0;
                return new CategoryStat
                {
                    Category = kv.Key,
                    QuantitySold = kv.Value.qty,
                    Revenue = kv.Value.rev,
                    BarRatio = ratio,
                    Percentage = ratio * 100.0
                };
            })
            .OrderByDescending(c => c.Revenue)
            .ToList();

        // Time buckets
        int count; Func<DateTime, int> index; Func<int, string> label;
        switch (period)
        {
            case ReportPeriod.Day:
                count = 24; index = d => d.Hour;
                label = i => new DateTime(2000, 1, 1, i, 0, 0).ToString("htt", CultureInfo.InvariantCulture).ToLowerInvariant();
                break;
            case ReportPeriod.Week:
                count = 7; index = d => ((int)d.DayOfWeek + 6) % 7;
                label = i => new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }[i];
                break;
            default:
                count = (end - start).Days; index = d => d.Day - 1;
                label = i => $"{start.AddDays(i):MMM d}";
                break;
        }

        for (var i = 0; i < count; i++) summary.Buckets.Add(new SalesBucket { Label = label(i) });
        foreach (var (order, local) in inRange)
        {
            var b = summary.Buckets[index(local)];
            b.Total += order.TotalAmount;
            b.Orders++;
        }
        return summary;
    }
}
