using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
#if ANDROID
using Android.Bluetooth;
using Java.Util;
#endif

namespace Wegs_Cafe.Services;

/// <summary>
/// Bluetooth ESC/POS thermal receipt printer service supporting 58mm (32 chars/line) and 80mm (48 chars/line) printers.
/// Uses Android RFCOMM SPP UUID 00001101-0000-1000-8000-00805F9B34FB on Android and simulated fallback on Windows.
/// </summary>
public class ReceiptPrinterService : IReceiptPrinterService
{
    private const string SppUuidString = "00001101-0000-1000-8000-00805F9B34FB";

    public bool IsSupported =>
#if ANDROID
        true;
#else
        false;
#endif

    public BluetoothPrinterDevice? SelectedPrinter { get; private set; }
    public int PaperWidthMillimeters { get; set; } = 58;
    public int CharactersPerLine => PaperWidthMillimeters >= 80 ? 48 : 32;

    public Task<IReadOnlyList<BluetoothPrinterDevice>> GetPairedPrintersAsync()
    {
        var devices = new List<BluetoothPrinterDevice>();

#if ANDROID
        try
        {
            var adapter = BluetoothAdapter.DefaultAdapter;
            if (adapter is { IsEnabled: true } && adapter.BondedDevices is not null)
            {
                foreach (var bonded in adapter.BondedDevices)
                {
                    var name = string.IsNullOrWhiteSpace(bonded.Name) ? "Bluetooth Printer" : bonded.Name;
                    var address = bonded.Address ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(address))
                    {
                        devices.Add(new BluetoothPrinterDevice(name, address));
                    }
                }
            }
        }
        catch
        {
            // Ignore Bluetooth permission/adapter exceptions and fall back to simulated list if empty.
        }
#endif

        if (devices.Count == 0)
        {
            devices.Add(new BluetoothPrinterDevice("Wegs Cafe 58mm Thermal Printer (Simulated)", "00:11:22:33:44:55"));
            devices.Add(new BluetoothPrinterDevice("Wegs Cafe 80mm Counter Printer (Simulated)", "00:11:22:33:44:66"));
        }

        SelectedPrinter ??= devices[0];
        return Task.FromResult<IReadOnlyList<BluetoothPrinterDevice>>(devices);
    }

    public void SelectPrinter(BluetoothPrinterDevice? printer)
    {
        SelectedPrinter = printer;
    }

    public async Task PrintOrderReceiptAsync(Order order)
    {
        var payload = ReceiptFormatter.BuildEscPosPayload(order, CharactersPerLine);
        await SendPayloadAsync(payload);
    }

    public async Task PrintTestReceiptAsync()
    {
        var testOrder = new Order
        {
            OrderIdentifier = "test_order",
            ReceiptNumber = "WEGS-TEST-0001",
            OrderType = OrderTypes.DineIn,
            CustomerOrTableLabel = "Printer Diagnostic",
            SubtotalAmount = 155m,
            DiscountAmount = 0m,
            TotalAmount = 155m,
            PaymentMethod = PaymentMethods.Cash,
            AmountTendered = 200m,
            ChangeDue = 45m,
            OrderStatus = OrderStatuses.Completed,
            CashierName = "System Diagnostic",
            CreatedAt = IsoTimestamp.Now(),
            OrderItems = new List<OrderItem>
            {
                new()
                {
                    ProductIdentifier = "test_item",
                    ProductName = "Spanish Latte (12oz)",
                    Quantity = 1,
                    UnitPrice = 155m,
                    LineSubtotal = 155m,
                    PreparationNotes = "Printer alignment test"
                }
            }
        };

        await PrintOrderReceiptAsync(testOrder);
    }

    private async Task SendPayloadAsync(byte[] payload)
    {
#if ANDROID
        if (SelectedPrinter is not null &&
            !SelectedPrinter.DeviceName.Contains("Simulated", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Run(() =>
            {
                var adapter = BluetoothAdapter.DefaultAdapter
                    ?? throw new InvalidOperationException("Bluetooth adapter is unavailable on this device.");

                var remoteDevice = adapter.GetRemoteDevice(SelectedPrinter.DeviceAddress)
                    ?? throw new InvalidOperationException("Unable to locate paired Bluetooth printer.");

                var uuid = UUID.FromString(SppUuidString)!;
                using var socket = remoteDevice.CreateRfcommSocketToServiceRecord(uuid)
                    ?? throw new InvalidOperationException("Could not open RFCOMM socket to printer.");

                adapter.CancelDiscovery();
                socket.Connect();

                using var outputStream = socket.OutputStream
                    ?? throw new InvalidOperationException("Printer output stream is unavailable.");

                outputStream.Write(payload, 0, payload.Length);
                outputStream.Flush();
            });
            return;
        }
#endif
        await Task.Delay(220);
    }
}
