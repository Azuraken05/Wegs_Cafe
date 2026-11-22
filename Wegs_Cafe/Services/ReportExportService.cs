using System.Text;
using ClosedXML.Excel;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Exports Sales Transactions and Inventory Valuation reports to Excel (.xlsx via ClosedXML) and CSV (.csv),
/// opens the system share sheet, and records an immutable EXPORT_SALES_REPORT audit log entry.
/// </summary>
public class ReportExportService : IReportExportService
{
    private readonly IAuditTrailService _auditTrailService;
    private readonly SessionService _sessionService;

    public ReportExportService(IAuditTrailService auditTrailService, SessionService sessionService)
    {
        _auditTrailService = auditTrailService;
        _sessionService = sessionService;
    }

    public async Task<string> ExportSalesReportToExcelAsync(
        IReadOnlyList<Order> orders,
        IReadOnlyList<Product> products,
        string timeframeLabel)
    {
        EnsureOwnerRole();

        var fileName = $"Wegs_Cafe_Sales_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();

            // Sheet 1: Sales Transactions
            var salesSheet = workbook.Worksheets.Add("Sales Transactions");
            var salesHeaders = new[]
            {
                "Receipt Number",
                "Timestamp (Local)",
                "Order Type",
                "Customer or Table",
                "Items Summary",
                "Subtotal (PHP)",
                "Discount (PHP)",
                "Total Amount (PHP)",
                "Payment Method",
                "Payment Reference Number",
                "Order Status",
                "Cashier Name"
            };

            for (var column = 0; column < salesHeaders.Length; column++)
            {
                var cell = salesSheet.Cell(1, column + 1);
                cell.Value = salesHeaders[column];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0A0A0A");
            }

            var rowNumber = 2;
            foreach (var order in orders)
            {
                var localTime = SalesAnalyticsCalculator.ParseLocalTimestamp(order.CreatedAt);
                var itemsSummary = string.Join("; ", order.OrderItems.Select(item => $"{item.Quantity}x {item.ProductName}"));

                salesSheet.Cell(rowNumber, 1).Value = order.ReceiptNumber;
                salesSheet.Cell(rowNumber, 2).Value = localTime.ToString("yyyy-MM-dd HH:mm:ss");
                salesSheet.Cell(rowNumber, 3).Value = order.OrderType;
                salesSheet.Cell(rowNumber, 4).Value = order.CustomerOrTableLabel;
                salesSheet.Cell(rowNumber, 5).Value = itemsSummary;
                salesSheet.Cell(rowNumber, 6).Value = order.SubtotalAmount;
                salesSheet.Cell(rowNumber, 7).Value = order.DiscountAmount;
                salesSheet.Cell(rowNumber, 8).Value = order.TotalAmount;
                salesSheet.Cell(rowNumber, 9).Value = order.PaymentMethod;
                salesSheet.Cell(rowNumber, 10).Value = order.PaymentReferenceNumber;
                salesSheet.Cell(rowNumber, 11).Value = order.OrderStatus;
                salesSheet.Cell(rowNumber, 12).Value = order.CashierName;

                salesSheet.Cell(rowNumber, 6).Style.NumberFormat.Format = "#,##0.00";
                salesSheet.Cell(rowNumber, 7).Style.NumberFormat.Format = "#,##0.00";
                salesSheet.Cell(rowNumber, 8).Style.NumberFormat.Format = "#,##0.00";
                rowNumber++;
            }

            salesSheet.Columns().AdjustToContents();

            // Sheet 2: Inventory Valuation
            var inventorySheet = workbook.Worksheets.Add("Inventory Valuation");
            var inventoryHeaders = new[]
            {
                "Stock Keeping Unit",
                "Product Name",
                "Category",
                "Stock Quantity",
                "Low Stock Threshold",
                "Measurement Unit",
                "Unit Cost Price (PHP)",
                "Selling Price (PHP)",
                "Total Stock Valuation (PHP)",
                "Stock Status"
            };

            for (var column = 0; column < inventoryHeaders.Length; column++)
            {
                var cell = inventorySheet.Cell(1, column + 1);
                cell.Value = inventoryHeaders[column];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0A0A0A");
            }

            var invRow = 2;
            foreach (var product in products)
            {
                var totalValuation = product.CostPrice * product.StockQuantity;
                inventorySheet.Cell(invRow, 1).Value = product.StockKeepingUnit;
                inventorySheet.Cell(invRow, 2).Value = product.ProductName;
                inventorySheet.Cell(invRow, 3).Value = product.Category;
                inventorySheet.Cell(invRow, 4).Value = product.StockQuantity;
                inventorySheet.Cell(invRow, 5).Value = product.LowStockThreshold;
                inventorySheet.Cell(invRow, 6).Value = product.MeasurementUnit;
                inventorySheet.Cell(invRow, 7).Value = product.CostPrice;
                inventorySheet.Cell(invRow, 8).Value = product.SellingPrice;
                inventorySheet.Cell(invRow, 9).Value = totalValuation;
                inventorySheet.Cell(invRow, 10).Value = product.StockStatusText;

                inventorySheet.Cell(invRow, 7).Style.NumberFormat.Format = "#,##0.00";
                inventorySheet.Cell(invRow, 8).Style.NumberFormat.Format = "#,##0.00";
                inventorySheet.Cell(invRow, 9).Style.NumberFormat.Format = "#,##0.00";
                invRow++;
            }

            inventorySheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        });

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.ExportSalesReport,
            $"reports/{fileName}",
            $"Exported Excel (.xlsx) Sales & Inventory Valuation Report ({timeframeLabel}) containing {orders.Count} orders.");

        await TryShareFileAsync(filePath, "Export Weg's Cafe Excel Report");
        return filePath;
    }

    public async Task<string> ExportSalesReportToCsvAsync(
        IReadOnlyList<Order> orders,
        IReadOnlyList<Product> products,
        string timeframeLabel)
    {
        EnsureOwnerRole();

        var fileName = $"Wegs_Cafe_Sales_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

        var builder = new StringBuilder();
        builder.AppendLine("Receipt Number,Timestamp,Order Type,Customer or Table,Items Summary,Subtotal,Discount,Total Amount,Payment Method,Payment Reference Number,Order Status,Cashier Name");

        foreach (var order in orders)
        {
            var localTime = SalesAnalyticsCalculator.ParseLocalTimestamp(order.CreatedAt);
            var itemsSummary = string.Join("; ", order.OrderItems.Select(item => $"{item.Quantity}x {item.ProductName}"));

            builder.AppendLine(string.Join(",",
                EscapeCsv(order.ReceiptNumber),
                EscapeCsv(localTime.ToString("yyyy-MM-dd HH:mm:ss")),
                EscapeCsv(order.OrderType),
                EscapeCsv(order.CustomerOrTableLabel),
                EscapeCsv(itemsSummary),
                order.SubtotalAmount.ToString("0.00"),
                order.DiscountAmount.ToString("0.00"),
                order.TotalAmount.ToString("0.00"),
                EscapeCsv(order.PaymentMethod),
                EscapeCsv(order.PaymentReferenceNumber),
                EscapeCsv(order.OrderStatus),
                EscapeCsv(order.CashierName)));
        }

        builder.AppendLine();
        builder.AppendLine("Stock Keeping Unit,Product Name,Category,Stock Quantity,Low Stock Threshold,Measurement Unit,Unit Cost Price,Selling Price,Total Stock Valuation,Stock Status");

        foreach (var product in products)
        {
            var valuation = product.CostPrice * product.StockQuantity;
            builder.AppendLine(string.Join(",",
                EscapeCsv(product.StockKeepingUnit),
                EscapeCsv(product.ProductName),
                EscapeCsv(product.Category),
                product.StockQuantity,
                product.LowStockThreshold,
                EscapeCsv(product.MeasurementUnit),
                product.CostPrice.ToString("0.00"),
                product.SellingPrice.ToString("0.00"),
                valuation.ToString("0.00"),
                EscapeCsv(product.StockStatusText)));
        }

        await File.WriteAllTextAsync(filePath, builder.ToString(), Encoding.UTF8);

        await _auditTrailService.RecordAuditEntryAsync(
            AuditActionTypes.ExportSalesReport,
            $"reports/{fileName}",
            $"Exported CSV Sales & Inventory Valuation Report ({timeframeLabel}) containing {orders.Count} orders.");

        await TryShareFileAsync(filePath, "Export Weg's Cafe CSV Report");
        return filePath;
    }

    private void EnsureOwnerRole()
    {
        if (!_sessionService.IsOwner)
        {
            throw new AuthenticationException("Only the Owner account is authorized to export sales and inventory reports.");
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static async Task TryShareFileAsync(string filePath, string shareTitle)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = shareTitle,
                    File = new ShareFile(filePath)
                });
            });
        }
        catch
        {
            // Share dialog may be unavailable on some desktop configurations; file path is still returned.
        }
    }
}
