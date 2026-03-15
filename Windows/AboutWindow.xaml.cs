using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Management;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace LenovoController
{
    public partial class AboutWindow : Wpf.Ui.Controls.FluentWindow
    {
        private string _serialNumber;
        private Uri _warrantyLink;

        // Lenovo PC Support API — same endpoints used by Lenovo Vantage and Legion Toolkit
        private const string ProductsApi = "https://pcsupport.lenovo.com/us/en/api/v4/mse/getproducts?productId={0}";
        private const string WarrantyApi = "https://pcsupport.lenovo.com/us/en/api/v4/upsell/redport/getWarranty?productId={0}&serialNumber={1}&countryCode=us&language=en";

        // Spoofs a real browser — Lenovo's API blocks non-browser user agents
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

        // ── Load all data ─────────────────────────────────────────────────────
        private async Task LoadAsync()
        {
            await LoadDeviceInfoAsync();
            await LoadWarrantyAsync();
        }

        // ── Device info from WMI ──────────────────────────────────────────────
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

        // ── Warranty from Lenovo API ──────────────────────────────────────────
        private async Task LoadWarrantyAsync()
        {
            Dispatcher.Invoke(() =>
            {
                txtWarrantyStart.Text = "Loading…";
                txtWarrantyEnd.Text   = "Loading…";
            });

            if (string.IsNullOrWhiteSpace(_serialNumber) || _serialNumber == "—")
            {
                Dispatcher.Invoke(() =>
                {
                    txtWarrantyStart.Text = "—";
                    txtWarrantyEnd.Text   = "—";
                });
                return;
            }

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Referer", "https://pcsupport.lenovo.com/");

                // Step 1: get product ID from serial number
                string productId = await GetProductIdAsync(client, _serialNumber);
                if (productId == null)
                    throw new Exception("Product not found for serial: " + _serialNumber);

                // Step 2: fetch warranty using product ID + serial
                string warrantyUrl = string.Format(WarrantyApi, productId, _serialNumber);
                string json = await client.GetStringAsync(warrantyUrl);

                var (start, end, link) = ParseWarranty(json, productId);

                _warrantyLink = link;

                Dispatcher.Invoke(() =>
                {
                    txtWarrantyStart.Text = start ?? "—";
                    txtWarrantyEnd.Text   = end   ?? "—";
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

        private static async Task<string> GetProductIdAsync(HttpClient client, string serial)
        {
            try
            {
                string url  = string.Format(ProductsApi, serial);
                string json = await client.GetStringAsync(url);
                var obj = JObject.Parse(json);

                // Response: { "data": { "productList": [ { "ProductID": "..." } ] } }
                var productId = obj["data"]?["productList"]?[0]?["ProductID"]?.ToString()
                             ?? obj["data"]?["productList"]?[0]?["id"]?.ToString();

                return productId;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"GetProductId: {ex.Message}");
                return null;
            }
        }

        private static (string start, string end, Uri link) ParseWarranty(string json, string productId)
        {
            try
            {
                var obj = JObject.Parse(json);

                // Traverse: data.WarrantyInfo[] or data.warrantyInfo[]
                var warrantyArray = obj["data"]?["WarrantyInfo"]
                                 ?? obj["data"]?["warrantyInfo"]
                                 ?? obj["data"]?["warranty"];

                string start = null, end = null;

                if (warrantyArray is JArray arr && arr.Count > 0)
                {
                    // Find the base warranty (type = 1 or earliest start date)
                    JToken best = arr[0];
                    foreach (var item in arr)
                    {
                        var typeVal = item["Type"]?.ToString() ?? item["type"]?.ToString();
                        if (typeVal == "1") { best = item; break; }
                    }

                    start = FormatDate(best["Start"]?.ToString() ?? best["start"]?.ToString());
                    end   = FormatDate(best["End"]?.ToString()   ?? best["end"]?.ToString());
                }

                // Build warranty link
                Uri link = null;
                try
                {
                    link = new Uri($"https://pcsupport.lenovo.com/us/en/products/{productId.ToLower()}/warranty");
                }
                catch { }

                return (start, end, link);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"ParseWarranty: {ex.Message}");
                return (null, null, null);
            }
        }

        private static string FormatDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            // Handle both "2019-08-25" and "08/25/2019" and unix timestamps
            if (long.TryParse(raw, out long ts))
                return DateTimeOffset.FromUnixTimeMilliseconds(ts)
                                     .LocalDateTime.ToString("M/d/yyyy");
            if (DateTime.TryParse(raw, out var dt))
                return dt.ToString("M/d/yyyy");
            return raw;
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
