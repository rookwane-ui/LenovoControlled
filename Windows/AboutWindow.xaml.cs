using System;
using System.Diagnostics;
using System.Management;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace LenovoController
{
    public partial class AboutWindow : Wpf.Ui.Controls.FluentWindow
    {
        private string _serialNumber;

        public AboutWindow(IntPtr ownerHandle)
        {
            InitializeComponent();
            new WindowInteropHelper(this).Owner = ownerHandle;
            _ = LoadDeviceInfoAsync();
        }

        // ── Device info ───────────────────────────────────────────────────────
        private async Task LoadDeviceInfoAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_ComputerSystemProduct");
                    using var results = searcher.Get();

                    foreach (ManagementObject obj in results)
                    {
                        string manufacturer = obj["Vendor"]?.ToString()?.Trim()
                                           ?? obj["Manufacturer"]?.ToString()?.Trim()
                                           ?? "—";
                        string model        = obj["Name"]?.ToString()?.Trim()     ?? "—";
                        string machineType  = obj["Version"]?.ToString()?.Trim()  ?? "—";
                        string serial       = obj["IdentifyingNumber"]?.ToString()?.Trim() ?? "—";

                        _serialNumber = serial;

                        // BIOS version from separate class
                        string bios = "—";
                        try
                        {
                            using var bs = new ManagementObjectSearcher(
                                "SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
                            using var br = bs.Get();
                            foreach (ManagementObject b in br)
                                bios = b["SMBIOSBIOSVersion"]?.ToString()?.Trim() ?? "—";
                        }
                        catch { }

                        Dispatcher.Invoke(() =>
                        {
                            txtManufacturer.Text = manufacturer.ToUpper();
                            txtModel.Text        = model;
                            txtMachineType.Text  = machineType;
                            txtSerial.Text       = MaskSerial(serial);
                            txtBios.Text         = bios;
                        });
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"AboutWindow.LoadDeviceInfo: {ex.Message}");
                }
            });

            await LoadWarrantyAsync();
        }

        // ── Warranty ──────────────────────────────────────────────────────────
        private async Task LoadWarrantyAsync()
        {
            if (string.IsNullOrWhiteSpace(_serialNumber) || _serialNumber == "—")
            {
                txtWarrantyStart.Text = "—";
                txtWarrantyEnd.Text   = "—";
                return;
            }

            txtWarrantyStart.Text = "Loading…";
            txtWarrantyEnd.Text   = "Loading…";

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("User-Agent", "LenovoController/2.0");

                // Lenovo warranty API
                string url = $"https://pcsupport.lenovo.com/us/en/api/v4/mse/getWarranty?country=us&language=en&serialnumber={_serialNumber}";
                var response = await client.GetStringAsync(url);

                // Parse start/end dates from JSON response
                var startMatch = Regex.Match(response, @"""Start""\s*:\s*""([^""]+)""");
                var endMatch   = Regex.Match(response, @"""End""\s*:\s*""([^""]+)""");

                string start = startMatch.Success ? FormatDate(startMatch.Groups[1].Value) : "—";
                string end   = endMatch.Success   ? FormatDate(endMatch.Groups[1].Value)   : "—";

                txtWarrantyStart.Text = start;
                txtWarrantyEnd.Text   = end;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"AboutWindow.LoadWarranty: {ex.Message}");
                txtWarrantyStart.Text = "Unavailable";
                txtWarrantyEnd.Text   = "Unavailable";
            }
        }

        private static string FormatDate(string raw)
        {
            if (DateTime.TryParse(raw, out var dt))
                return dt.ToString("M/d/yyyy");
            return raw;
        }

        // Mask middle of serial for privacy when displayed
        private static string MaskSerial(string serial)
        {
            if (serial.Length <= 4) return serial;
            return serial; // show full serial — remove this line to mask
        }

        // ── Handlers ──────────────────────────────────────────────────────────
        private async void BtnRefreshWarranty_Click(object sender, RoutedEventArgs e)
        {
            await LoadWarrantyAsync();
        }

        private void BtnLenovoSupport_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                string url = string.IsNullOrWhiteSpace(_serialNumber) || _serialNumber == "—"
                    ? "https://support.lenovo.com"
                    : $"https://pcsupport.lenovo.com/us/en/products/laptops-and-netbooks/ideapad/ideapad-l340-series/{_serialNumber}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"AboutWindow.LenovoSupport: {ex.Message}");
            }
        }
    }
}
