using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace LenovoController
{
    public partial class AboutWindow : Wpf.Ui.Controls.FluentWindow
    {
        private string _serialNumber;
        private string _machineType;
        private Uri _warrantyLink;

        // Exact same endpoint Legion Toolkit uses
        private const string IbaseInfoUrl = "https://pcsupport.lenovo.com/dk/en/api/v4/upsell/redport/getIbaseInfo";
        private const string ProductsUrl   = "https://pcsupport.lenovo.com/dk/en/api/v4/mse/getproducts?productId={0}";

        private const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/124.0.0.0 Safari/537.36";

        public AboutWindow(IntPtr ownerHandle)
        {
            InitializeComponent();
            new WindowInteropHelper(this).Owner = ownerHandle;
            _ = LoadAsync();
        }

        // ── Load everything ───────────────────────────────────────────────────
        private async Task LoadAsync()
        {
            await LoadDeviceInfoAsync();
            await LoadWarrantyAsync();
        }

        // ── WMI device info ───────────────────────────────────────────────────
        private async Task LoadDeviceInfoAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    string manufacturer = "—", model = "—", machineType = "—",
                           serial = "—", bios = "—";

                    using (var s = new ManagementObjectSearcher(
                               "SELECT * FROM Win32_ComputerSystemProduct"))
                    using (var r = s.Get())
                    {
                        foreach (ManagementObject o in r)
                        {
                            manufacturer = o["Vendor"]?.ToString()?.Trim()
                                        ?? o["Manufacturer"]?.ToString()?.Trim() ?? "—";
                            model        = o["Name"]?.ToString()?.Trim()              ?? "—";
                            machineType  = o["Version"]?.ToString()?.Trim()           ?? "—";
                            serial       = o["IdentifyingNumber"]?.ToString()?.Trim() ?? "—";
                            break;
                        }
                    }

                    using (var s = new ManagementObjectSearcher(
                               "SELECT SMBIOSBIOSVersion FROM Win32_BIOS"))
                    using (var r = s.Get())
                    {
                        foreach (ManagementObject o in r)
                        {
                            bios = o["SMBIOSBIOSVersion"]?.ToString()?.Trim() ?? "—";
                            break;
                        }
                    }

                    _serialNumber = serial;
                    // MachineType is the first 4 chars of the model version (e.g. "81LK")
                    _machineType  = machineType?.Length >= 4
                                    ? machineType.Substring(0, 4)
                                    : machineType;

                    Dispatcher.Invoke(() =>
                    {
                        txtManufacturer.Text = manufacturer.ToUpperInvariant();
                        txtModel.Text        = model;
                        txtMachineType.Text  = machineType;
                        txtSerial.Text       = serial;
                        txtBios.Text         = bios;
                    });
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"AboutWindow.LoadDeviceInfo: {ex.Message}");
                }
            });
        }

        // ── Warranty — same logic as Legion Toolkit WarrantyChecker.cs ────────
        private async Task LoadWarrantyAsync()
        {
            Dispatcher.Invoke(() =>
            {
                txtWarrantyStart.Text = "Loading…";
                txtWarrantyEnd.Text   = "Loading…";
            });

            if (string.IsNullOrWhiteSpace(_serialNumber) || _serialNumber == "—")
            {
                Dispatcher.Invoke(() => { txtWarrantyStart.Text = "—"; txtWarrantyEnd.Text = "—"; });
                return;
            }

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                // ── Step 1: POST getIbaseInfo (Legion Toolkit's exact call) ──
                var body    = new StringContent(
                    $"{{\"serialNumber\":\"{_serialNumber}\",\"machineType\":\"{_machineType}\"}}",
                    Encoding.UTF8, "application/json");

                var response = await client.PostAsync(IbaseInfoUrl, body);
                var json     = await response.Content.ReadAsStringAsync();
                var node     = JObject.Parse(json);

                if (node["code"]?.Value<int>() != 0)
                    throw new Exception($"getIbaseInfo code={node["code"]} msg={node["msg"]}");

                var baseWarranties    = node["data"]?["baseWarranties"]?.ToObject<JArray>()    ?? new JArray();
                var upgradeWarranties = node["data"]?["upgradeWarranties"]?.ToObject<JArray>() ?? new JArray();
                var all = baseWarranties.Concat(upgradeWarranties).ToList();

                // Min start date, max end date — same as Legion Toolkit
                var startDates = all
                    .Select(n => n?["startDate"]?.ToString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => DateTime.Parse(s!))
                    .ToList();

                var endDates = all
                    .Select(n => n?["endDate"]?.ToString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => DateTime.Parse(s!))
                    .ToList();

                string startText = startDates.Any() ? startDates.Min().ToString("M/d/yyyy") : "—";
                string endText   = endDates.Any()   ? endDates.Max().ToString("M/d/yyyy")   : "—";

                // ── Step 2: GET product ID for support link ──────────────────
                try
                {
                    var productJson = await client.GetStringAsync(
                        string.Format(ProductsUrl, _serialNumber));
                    var productNode = JArray.Parse(productJson);
                    var id = productNode.FirstOrDefault()?["Id"]?.ToString();
                    if (id != null)
                        _warrantyLink = new Uri($"https://pcsupport.lenovo.com/products/{id}");
                }
                catch { /* link is optional */ }

                Dispatcher.Invoke(() =>
                {
                    txtWarrantyStart.Text = startText;
                    txtWarrantyEnd.Text   = endText;
                });
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"AboutWindow.LoadWarranty: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    txtWarrantyStart.Text = "Unavailable";
                    txtWarrantyEnd.Text   = "Unavailable";
                });
            }
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
                string url = _warrantyLink?.ToString()
                          ?? (_serialNumber != null && _serialNumber != "—"
                              ? $"https://pcsupport.lenovo.com/us/en/warrantylookup#/{_serialNumber}"
                              : "https://support.lenovo.com");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"LenovoSupport: {ex.Message}");
            }
        }
    }
}
