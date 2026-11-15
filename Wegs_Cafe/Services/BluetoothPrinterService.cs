using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;
#if ANDROID
using Android.Bluetooth;
#endif

namespace Wegs_Cafe.Services;

/// <summary>
/// ESC/POS receipt printing over Bluetooth. Android: classic Bluetooth (SPP/RFCOMM) to a PAIRED printer -
/// this is how nearly all 58mm/80mm thermal printers work. Pair the printer in Android settings first.
/// Other platforms: not supported (use the PDF / share fallback).
/// </summary>
public class BluetoothPrinterService : IReceiptPrinterService
{
    private const string AddressKey = "printer_address";
    private const string NameKey = "printer_name";
    private const string WidthKey = "printer_width_mm";

    public bool IsSupported =>
#if ANDROID
        true;
#else
        false;
#endif

    public PrinterDevice? SelectedPrinter
    {
        get
        {
            var address = Preferences.Get(AddressKey, "");
            return string.IsNullOrEmpty(address) ? null : new PrinterDevice(Preferences.Get(NameKey, address), address);
        }
    }

    public int PaperWidthMm
    {
        get => Preferences.Get(WidthKey, 58) == 80 ? 80 : 58;
        set => Preferences.Set(WidthKey, value == 80 ? 80 : 58);
    }

    public int CharsPerLine => PaperWidthMm == 80 ? 48 : 32;

    public void SelectPrinter(PrinterDevice? printer)
    {
        if (printer == null) { Preferences.Remove(AddressKey); Preferences.Remove(NameKey); return; }
        Preferences.Set(AddressKey, printer.Address);
        Preferences.Set(NameKey, printer.Name);
    }

    public Task PrintAsync(Order order)
        => SendAsync(ReceiptBuilder.ToEscPos(ReceiptBuilder.Build(order, CharsPerLine, "PHP ")));

    public Task PrintTestAsync()
        => SendAsync(ReceiptBuilder.ToEscPos(ReceiptBuilder.BuildTest(CharsPerLine)));

    private Task SendAsync(byte[] data)
    {
        var printer = SelectedPrinter ?? throw new InvalidOperationException("No printer selected. Choose one in Printer settings.");
        return SendToAsync(printer.Address, data);
    }

#if ANDROID
    private static async Task EnsurePermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
        if (status != PermissionStatus.Granted) status = await Permissions.RequestAsync<Permissions.Bluetooth>();
        if (status != PermissionStatus.Granted) throw new InvalidOperationException("Bluetooth permission was denied.");
    }

    private static BluetoothAdapter GetAdapter()
    {
        var adapter = BluetoothAdapter.DefaultAdapter ?? throw new InvalidOperationException("This device has no Bluetooth.");
        if (!adapter.IsEnabled) throw new InvalidOperationException("Turn Bluetooth on first.");
        return adapter;
    }

    public async Task<IReadOnlyList<PrinterDevice>> GetPairedPrintersAsync()
    {
        await EnsurePermissionAsync();
        var adapter = GetAdapter();
        var devices = adapter.BondedDevices;
        if (devices == null) return Array.Empty<PrinterDevice>();
        return devices.Select(d => new PrinterDevice(d.Name ?? d.Address ?? "Unknown", d.Address ?? "")).OrderBy(d => d.Name).ToList();
    }

    private static async Task SendToAsync(string address, byte[] data)
    {
        await EnsurePermissionAsync();
        var adapter = GetAdapter();
        var device = adapter.GetRemoteDevice(address) ?? throw new InvalidOperationException("Printer not found.");
        adapter.CancelDiscovery();

        // Standard Serial Port Profile UUID used by thermal printers
        using var socket = device.CreateRfcommSocketToServiceRecord(Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB"))
                           ?? throw new InvalidOperationException("Could not open the printer connection.");
        try
        {
            await socket.ConnectAsync();
            var stream = socket.OutputStream ?? throw new InvalidOperationException("Printer connection has no output.");
            // send in small chunks - cheap printers have tiny buffers
            for (var i = 0; i < data.Length; i += 512)
            {
                var len = Math.Min(512, data.Length - i);
                await stream.WriteAsync(data, i, len);
                await stream.FlushAsync();
                await Task.Delay(30);
            }
            await Task.Delay(600);   // let the printer finish before closing
        }
        catch (Java.IO.IOException ex)
        {
            throw new InvalidOperationException("Could not reach the printer. Is it on, in range and paired?  (" + ex.Message + ")");
        }
        finally { socket.Close(); }
    }
#else
    public Task<IReadOnlyList<PrinterDevice>> GetPairedPrintersAsync()
        => throw new NotSupportedException("Bluetooth printing is only available on Android.");

    private static Task SendToAsync(string address, byte[] data)
        => throw new NotSupportedException("Bluetooth printing is only available on Android.");
#endif
}
