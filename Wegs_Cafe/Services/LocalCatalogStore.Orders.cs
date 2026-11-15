using System.Globalization;
using System.Text.RegularExpressions;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>Orders + shifts. Shares the catalog lock so checkout (order + stock + logs) is one atomic step.</summary>
public partial class LocalCatalogStore : IOrderService, IShiftService
{
    private readonly string _ordersPath = Path.Combine(FileSystem.AppDataDirectory, "local_orders.json");
    private readonly string _shiftsPath = Path.Combine(FileSystem.AppDataDirectory, "local_shifts.json");

    // ================= Checkout =================
    public async Task<Order> CheckoutAsync(CheckoutRequest req)
    {
        if (!_session.CanUsePos) throw new UnauthorizedAccessException("Please sign in.");
        if (req.Items.Count == 0) throw new ValidationException("The cart is empty.");
        if (req.OrderType != OrderTypes.DineIn && req.OrderType != OrderTypes.TakeOut) throw new ValidationException("Choose Dine-In or Take-Out.");

        await _lock.WaitAsync();
        try
        {
            var products = await JsonFile.ReadAsync<Product>(_productsPath);
            var orders = await JsonFile.ReadAsync<Order>(_ordersPath);

            // Prices and stock always come from the saved product, never from the screen.
            var lines = new List<OrderItem>();
            var deductions = new List<(Product product, int qty)>();
            foreach (var cart in req.Items)
            {
                var product = products.FirstOrDefault(p => p.ProductId == cart.Product.ProductId)
                              ?? throw new ValidationException($"\"{cart.Product.Name}\" no longer exists.");
                if (cart.Qty <= 0) throw new ValidationException("Quantity must be at least 1.");
                if (!product.IsAvailable) throw new ValidationException($"\"{product.Name}\" is not available.");
                if (product.StockQuantity < cart.Qty)
                    throw new ValidationException($"Only {product.StockQuantity} {product.Unit} of \"{product.Name}\" left.");

                lines.Add(new OrderItem
                {
                    ProductId = product.ProductId,
                    Name = product.Name,
                    Qty = cart.Qty,
                    UnitPrice = product.Price,
                    Subtotal = product.Price * cart.Qty,
                    Notes = cart.Notes?.Trim() ?? string.Empty
                });
                deductions.Add((product, cart.Qty));
            }

            var subtotal = lines.Sum(l => l.Subtotal);
            var discount = req.DiscountAmount;
            if (discount < 0 || discount > subtotal) throw new ValidationException("Discount must be between 0 and the subtotal.");
            if (discount > 0 && !_session.CanManageProducts) throw new UnauthorizedAccessException("Only a manager or the owner can give discounts.");
            var total = subtotal - discount;

            decimal tendered, change;
            string reference = req.PaymentReference?.Trim() ?? string.Empty, last4 = req.CardLast4?.Trim() ?? string.Empty;
            switch (req.PaymentMethod)
            {
                case PaymentMethods.Cash:
                    if (req.AmountTendered < total) throw new ValidationException("Cash received is less than the total.");
                    tendered = req.AmountTendered; change = tendered - total; reference = last4 = string.Empty;
                    break;
                case PaymentMethods.GCash:
                    if (reference.Length < 4) throw new ValidationException("Enter the GCash reference number.");
                    tendered = total; change = 0; last4 = string.Empty;
                    break;
                case PaymentMethods.CreditCard:
                    if (reference.Length < 4) throw new ValidationException("Enter the card approval / trace number.");
                    if (!Regex.IsMatch(last4, "^[0-9]{4}$")) throw new ValidationException("Enter the last 4 digits of the card.");
                    tendered = total; change = 0;
                    break;
                default:
                    throw new ValidationException("Choose a payment method.");
            }

            var today = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var seq = orders.Count(o => o.ReceiptNumber.StartsWith($"WEGS-{today}-")) + 1;

            var order = new Order
            {
                OrderId = Guid.NewGuid().ToString("N"),
                ReceiptNumber = $"WEGS-{today}-{seq:0000}",
                OrderType = req.OrderType,
                TableOrCustomer = req.TableOrCustomer?.Trim() ?? string.Empty,
                Items = lines,
                Subtotal = subtotal,
                DiscountAmount = discount,
                TotalAmount = total,
                PaymentMethod = req.PaymentMethod,
                PaymentReference = reference,
                CardLast4 = last4,
                AmountTendered = tendered,
                ChangeDue = change,
                Status = OrderStatuses.Completed,
                CashierUid = _session.CurrentUser!.Uid,
                CashierName = _session.CurrentUser!.FullName
            };
            orders.Add(order);

            // Deduct stock + inventory logs
            var logs = await JsonFile.ReadAsync<InventoryLog>(_logsPath);
            foreach (var (product, qty) in deductions)
            {
                var previous = product.StockQuantity;
                product.StockQuantity = previous - qty;
                product.UpdatedAt = IsoDate.Now();
                logs.Add(NewLog(product, InventoryChangeTypes.SaleDeduction, -qty, previous, product.StockQuantity));
            }

            await JsonFile.WriteAsync(_ordersPath, orders);
            await JsonFile.WriteAsync(_productsPath, products);
            await JsonFile.WriteAsync(_logsPath, logs);
            await _audit.LogAsync(AuditActions.PosCheckout, $"orders/{order.OrderId}",
                $"{order.ReceiptNumber}: ₱{order.TotalAmount:N2} via {order.PaymentMethod}, {lines.Sum(l => l.Qty)} item(s).");

            RaiseStockChanged();
            return order;
        }
        finally { _lock.Release(); }
    }

    // ================= History / Void =================
    public async Task<IReadOnlyList<Order>> GetOrdersAsync()
    {
        RequireLogin();
        await _lock.WaitAsync();
        try
        {
            var all = await EnsureOrdersSeededAsync();
            var visible = _session.IsOwner || _session.IsManager ? all : all.Where(o => o.CashierUid == _session.CurrentUser!.Uid).ToList();
            return visible.OrderByDescending(o => o.CreatedAt).ToList();
        }
        finally { _lock.Release(); }
    }

    private async Task<List<Order>> EnsureOrdersSeededAsync()
    {
        if (File.Exists(_ordersPath))
        {
            var existing = await JsonFile.ReadAsync<Order>(_ordersPath);
            if (existing.Count > 0) return existing;
        }

        var now = DateTime.Now;
        Order Sample(int seq, DateTime whenLocal, string type, string table, string method, string refNo, params (string id, string name, int qty, decimal price)[] items)
        {
            var lines = items.Select(i => new OrderItem
            {
                ProductId = i.id,
                Name = i.name,
                Qty = i.qty,
                UnitPrice = i.price,
                Subtotal = i.qty * i.price
            }).ToList();
            var sub = lines.Sum(l => l.Subtotal);
            return new Order
            {
                OrderId = Guid.NewGuid().ToString("N"),
                ReceiptNumber = $"WEGS-{whenLocal:yyyyMMdd}-{seq:0000}",
                OrderType = type,
                TableOrCustomer = table,
                Items = lines,
                Subtotal = sub,
                TotalAmount = sub,
                PaymentMethod = method,
                PaymentReference = refNo,
                AmountTendered = sub,
                ChangeDue = 0,
                Status = OrderStatuses.Completed,
                CashierUid = "local-owner",
                CashierName = "Owner",
                CreatedAt = IsoDate.From(whenLocal)
            };
        }

        var today = now.Date;
        var seeded = new List<Order>
        {
            Sample(1, today.AddHours(8).AddMinutes(15), OrderTypes.DineIn, "Table 2", PaymentMethods.Cash, "",
                ("prod-espresso", "Spanish Latte", 2, 140m), ("prod-croissant", "Butter Croissant", 2, 95m)),
            Sample(2, today.AddHours(9).AddMinutes(40), OrderTypes.TakeOut, "Walk-in", PaymentMethods.GCash, "901284112",
                ("prod-americano", "Iced Americano", 3, 110m), ("prod-oatmilk", "Sub Oat Milk", 1, 35m)),
            Sample(3, today.AddHours(11).AddMinutes(20), OrderTypes.DineIn, "Table 5", PaymentMethods.CreditCard, "482910",
                ("prod-pasta", "Truffle Cream Pasta", 2, 220m), ("prod-matcha", "Dirty Matcha Latte", 2, 160m)),
            Sample(4, today.AddHours(13).AddMinutes(5), OrderTypes.DineIn, "Table 1", PaymentMethods.Cash, "",
                ("prod-tapa", "Wegs Beef Tapa Bowl", 2, 195m), ("prod-caramel", "Caramel Macchiato", 2, 155m)),
            Sample(5, today.AddHours(15).AddMinutes(30), OrderTypes.TakeOut, "Grab Pick-up", PaymentMethods.GCash, "901299871",
                ("prod-cheesecake", "Burnt Basque Cheesecake", 2, 175m), ("prod-espresso", "Spanish Latte", 1, 140m)),
            Sample(1, today.AddDays(-1).AddHours(10), OrderTypes.DineIn, "Table 3", PaymentMethods.Cash, "",
                ("prod-espresso", "Spanish Latte", 3, 140m), ("prod-pasta", "Truffle Cream Pasta", 1, 220m)),
            Sample(2, today.AddDays(-1).AddHours(14), OrderTypes.TakeOut, "Walk-in", PaymentMethods.GCash, "881920114",
                ("prod-matcha", "Dirty Matcha Latte", 2, 160m), ("prod-croissant", "Butter Croissant", 3, 95m)),
            Sample(1, today.AddDays(-2).AddHours(11), OrderTypes.DineIn, "Table 4", PaymentMethods.CreditCard, "771209",
                ("prod-tapa", "Wegs Beef Tapa Bowl", 3, 195m), ("prod-americano", "Iced Americano", 3, 110m)),
            Sample(1, today.AddDays(-3).AddHours(16), OrderTypes.DineIn, "Table 2", PaymentMethods.Cash, "",
                ("prod-caramel", "Caramel Macchiato", 4, 155m), ("prod-cheesecake", "Burnt Basque Cheesecake", 2, 175m))
        };

        await JsonFile.WriteAsync(_ordersPath, seeded);
        return seeded;
    }

    public async Task<Order> VoidOrderAsync(string orderId, string authorizerPin)
    {
        RequireLogin();
        var authorizer = await _auth.AuthorizeManagerPinAsync(authorizerPin);   // throws AuthException on wrong PIN

        await _lock.WaitAsync();
        try
        {
            var orders = await JsonFile.ReadAsync<Order>(_ordersPath);
            var order = orders.FirstOrDefault(o => o.OrderId == orderId) ?? throw new ValidationException("Order not found.");
            if (order.Status == OrderStatuses.Voided) throw new ValidationException("This order is already voided.");
            if (!_session.IsOwner && !_session.IsManager && order.CashierUid != _session.CurrentUser!.Uid)
                throw new UnauthorizedAccessException("You can only void your own orders.");

            var products = await JsonFile.ReadAsync<Product>(_productsPath);
            var logs = await JsonFile.ReadAsync<InventoryLog>(_logsPath);
            foreach (var item in order.Items)
            {
                var product = products.FirstOrDefault(p => p.ProductId == item.ProductId);
                if (product == null) continue;                       // product deleted since - nothing to restore
                var previous = product.StockQuantity;
                product.StockQuantity = previous + item.Qty;
                product.UpdatedAt = IsoDate.Now();
                logs.Add(NewLog(product, InventoryChangeTypes.ManualAdjustment, item.Qty, previous, product.StockQuantity));
            }

            order.Status = OrderStatuses.Voided;
            order.VoidedAt = IsoDate.Now();
            order.VoidedByUid = authorizer.Uid;

            await JsonFile.WriteAsync(_ordersPath, orders);
            await JsonFile.WriteAsync(_productsPath, products);
            await JsonFile.WriteAsync(_logsPath, logs);
            await _audit.LogAsync(AuditActions.VoidOrder, $"orders/{order.OrderId}",
                $"Voided {order.ReceiptNumber} (₱{order.TotalAmount:N2}); stock restored. Authorized by {authorizer.FullName} ({authorizer.Role}).");

            RaiseStockChanged();
            return order;
        }
        finally { _lock.Release(); }
    }

    private InventoryLog NewLog(Product p, string type, int change, int previous, int next) => new()
    {
        LogId = Guid.NewGuid().ToString("N"),
        ProductId = p.ProductId,
        ProductName = p.Name,
        ChangeType = type,
        QuantityChanged = change,
        PreviousStock = previous,
        NewStock = next,
        PerformedByUid = _session.CurrentUser?.Uid ?? "",
        PerformedByName = _session.CurrentUser?.FullName ?? ""
    };

    // ================= Shifts (X / Z reading) =================
    public async Task<Shift?> GetOpenShiftAsync()
    {
        RequireLogin();
        await _lock.WaitAsync();
        try { return (await JsonFile.ReadAsync<Shift>(_shiftsPath)).FirstOrDefault(s => s.Status == "open"); }
        finally { _lock.Release(); }
    }

    public async Task<Shift> OpenShiftAsync(decimal openingCash)
    {
        RequireLogin();
        if (openingCash < 0) throw new ValidationException("Opening cash cannot be negative.");
        await _lock.WaitAsync();
        try
        {
            var shifts = await JsonFile.ReadAsync<Shift>(_shiftsPath);
            if (shifts.Any(s => s.Status == "open")) throw new ValidationException("A shift is already open.");
            var shift = new Shift
            {
                ShiftId = Guid.NewGuid().ToString("N"),
                OpenedByUid = _session.CurrentUser!.Uid,
                OpenedByName = _session.CurrentUser!.FullName,
                OpeningCash = openingCash
            };
            shifts.Add(shift);
            await JsonFile.WriteAsync(_shiftsPath, shifts);
            await _audit.LogAsync("SHIFT_OPEN", $"shifts/{shift.ShiftId}", $"Opened shift with ₱{openingCash:N2} cash.");
            return shift;
        }
        finally { _lock.Release(); }
    }

    public async Task<ShiftReading> GetReadingAsync(Shift shift)
    {
        RequireLogin();
        await _lock.WaitAsync();
        try { return await ComputeReadingAsync(shift); }
        finally { _lock.Release(); }
    }

    private async Task<ShiftReading> ComputeReadingAsync(Shift shift)
    {
        var orders = (await JsonFile.ReadAsync<Order>(_ordersPath))
            .Where(o => string.CompareOrdinal(o.CreatedAt, shift.OpenedAt) >= 0
                        && (string.IsNullOrEmpty(shift.ClosedAt) || string.CompareOrdinal(o.CreatedAt, shift.ClosedAt) <= 0))
            .ToList();
        var done = orders.Where(o => o.Status == OrderStatuses.Completed).ToList();
        return new ShiftReading
        {
            OpeningCash = shift.OpeningCash,
            OrderCount = done.Count,
            VoidedCount = orders.Count - done.Count,
            TotalSales = done.Sum(o => o.TotalAmount),
            CashSales = done.Where(o => o.PaymentMethod == PaymentMethods.Cash).Sum(o => o.TotalAmount),
            GCashSales = done.Where(o => o.PaymentMethod == PaymentMethods.GCash).Sum(o => o.TotalAmount),
            CardSales = done.Where(o => o.PaymentMethod == PaymentMethods.CreditCard).Sum(o => o.TotalAmount)
        };
    }

    public async Task<Shift> CloseShiftAsync(decimal actualCash)
    {
        RequireLogin();
        if (actualCash < 0) throw new ValidationException("Counted cash cannot be negative.");
        await _lock.WaitAsync();
        try
        {
            var shifts = await JsonFile.ReadAsync<Shift>(_shiftsPath);
            var shift = shifts.FirstOrDefault(s => s.Status == "open") ?? throw new ValidationException("There is no open shift.");

            shift.ClosedAt = IsoDate.Now();
            var reading = await ComputeReadingAsync(shift);
            shift.Status = "closed";
            shift.ClosedByUid = _session.CurrentUser!.Uid;
            shift.ClosedByName = _session.CurrentUser!.FullName;
            shift.OrderCount = reading.OrderCount;
            shift.TotalSales = reading.TotalSales;
            shift.CashSales = reading.CashSales;
            shift.GCashSales = reading.GCashSales;
            shift.CardSales = reading.CardSales;
            shift.ExpectedCash = reading.ExpectedCash;
            shift.ActualCash = actualCash;
            shift.Difference = actualCash - reading.ExpectedCash;
            await JsonFile.WriteAsync(_shiftsPath, shifts);

            await _audit.LogAsync(AuditActions.ShiftClose, $"shifts/{shift.ShiftId}",
                $"Z reading: sales ₱{shift.TotalSales:N2} ({shift.OrderCount} orders), expected cash ₱{shift.ExpectedCash:N2}, counted ₱{shift.ActualCash:N2}.");
            if (shift.Difference != 0)
                await _audit.LogAsync(AuditActions.ShiftDiscrepancy, $"shifts/{shift.ShiftId}",
                    $"Cash {(shift.Difference < 0 ? "SHORT" : "OVER")} by ₱{Math.Abs(shift.Difference):N2} (expected ₱{shift.ExpectedCash:N2}, counted ₱{shift.ActualCash:N2}).");
            return shift;
        }
        finally { _lock.Release(); }
    }
}
