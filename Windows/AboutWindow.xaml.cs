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
using Microsoft.Win32;

namespace LenovoController
{
    public partial class AboutWindow : Wpf.Ui.Controls.FluentWindow
    {
        private string _serialNumber;
        private string _machineType;
        private Uri _warrantyLink;

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

        // ── Load all sections ─────────────────────────────────────────────────
        private async Task LoadAsync()
        {
            await LoadDeviceInfoAsync();
            LoadWindowsInfo();
            LoadBatteryInfo();
            await LoadWarrantyAsync();
        }

        // ── Device info (WMI) ─────────────────────────────────────────────────
        private async Task LoadDeviceInfoAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    string manufacturer = "—", model = "—", machineType = "—",
                           serial = "—", bios = "—";

                    using (var s = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystemProduct"))
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

                    using (var s = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS"))
                    using (var r = s.Get())
                    {
                        foreach (ManagementObject o in r)
                        {
                            bios = o["SMBIOSBIOSVersion"]?.ToString()?.Trim() ?? "—";
                            break;
                        }
                    }

                    _serialNumber = serial;
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
                    Trace.TraceWarning($"LoadDeviceInfo: {ex.Message}");
                }
            });
        }

        // ── Windows version + exact build from Registry ───────────────────────
        private void LoadWindowsInfo()
        {
            try
            {
                const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
                using var reg = Registry.LocalMachine.OpenSubKey(key);
                if (reg == null) return;

                // e.g. "Windows 11 Home" or "Windows 11 Pro"
                string edition = reg.GetValue("ProductName")?.ToString() ?? "—";

                // e.g. "23H2"
                string displayVersion = reg.GetValue("DisplayVersion")?.ToString()
                                     ?? reg.GetValue("ReleaseId")?.ToString()
                                     ?? "—";

                // e.g. "22631" (major OS build)
                string currentBuild = reg.GetValue("CurrentBuild")?.ToString() ?? "—";

                // UBR = Update Build Revision (the .xxxx part) — gives exact patch build
                string ubr = reg.GetValue("UBR")?.ToString() ?? "";
                string fullBuild = string.IsNullOrEmpty(ubr)
                                   ? currentBuild
                                   : $"{currentBuild}.{ubr}";

                txtWinEdition.Text  = edition;
                txtWinVersion.Text  = displayVersion;
                txtWinBuild.Text    = fullBuild;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"LoadWindowsInfo: {ex.Message}");
                txtWinEdition.Text = txtWinVersion.Text = txtWinBuild.Text = "—";
            }
        }

        // ── Battery health via WMI BatteryFullChargedCapacity ─────────────────
        private void LoadBatteryInfo()
        {
            try
            {
                ulong designCap = 0, fullCap = 0;
                uint  cycles    = 0;

                // Design capacity
                using (var s = new ManagementObjectSearcher(
                    "root\\WMI", "SELECT * FROM BatteryStaticData"))
                using (var r = s.Get())
                {
                    foreach (ManagementObject o in r)
                    {
                        designCap = (ulong)(o["DesignedCapacity"] ?? 0UL);
                        break;
                    }
                }

                // Full charge capacity + cycle count
                using (var s = new ManagementObjectSearcher(
                    "root\\WMI", "SELECT * FROM BatteryFullChargedCapacity"))
                using (var r = s.Get())
                {
                    foreach (ManagementObject o in r)
                    {
                        fullCap = (ulong)(o["FullChargedCapacity"] ?? 0UL);
                        break;
                    }
                }

                using (var s = new ManagementObjectSearcher(
                    "root\\WMI", "SELECT * FROM BatteryCycleCount"))
                using (var r = s.Get())
                {
                    foreach (ManagementObject o in r)
                    {
                        cycles = (uint)(o["CycleCount"] ?? 0u);
                        break;
                    }
                }

                // Calculate health %
                double healthPct = (designCap > 0 && fullCap > 0)
                    ? Math.Round((double)fullCap / designCap * 100.0, 1)
                    : 0;

                // Pick bar colour: green ≥70%, yellow ≥40%, red <40%
                string barColor = healthPct >= 70 ? "#4CAF50"
                                : healthPct >= 40 ? "#FFC107"
                                                  : "#F44336";

                // mWh → Wh for display
                string FormatCap(ulong mwh) =>
                    mwh > 0 ? $"{mwh / 1000.0:F1} Wh  ({mwh:N0} mWh)" : "—";

                Dispatcher.Invoke(() =>
                {
                    if (healthPct > 0)
                    {
                        txtBatteryHealth.Text    = $"{healthPct:F1}%";
                        batteryHealthBar.Width   = Math.Min(80, 80 * healthPct / 100.0);
                        batteryHealthBar.Background =
                            new System.Windows.Media.SolidColorBrush(
                                (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                    .ConvertFromString(barColor));
                    }
                    else
                    {
                        txtBatteryHealth.Text  = "—";
                        batteryHealthBar.Width = 0;
                    }

                    txtBatteryDesign.Text  = FormatCap(designCap);
                    txtBatteryFull.Text    = FormatCap(fullCap);
                    txtBatteryCycles.Text  = cycles > 0 ? $"{cycles} cycles" : "—";
                });
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"LoadBatteryInfo: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    txtBatteryHealth.Text = txtBatteryDesign.Text =
                    txtBatteryFull.Text   = txtBatteryCycles.Text = "—";
                });
            }
        }

        // ── Warranty (Legion Toolkit approach) ────────────────────────────────
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

                var body = new StringContent(
                    $"{{\"serialNumber\":\"{_serialNumber}\",\"machineType\":\"{_machineType}\"}}",
                    Encoding.UTF8, "application/json");

                var response = await client.PostAsync(IbaseInfoUrl, body);
                var json     = await response.Content.ReadAsStringAsync();
                var node     = JObject.Parse(json);

                if (node["code"]?.Value<int>() != 0)
                    throw new Exception($"getIbaseInfo code={node["code"]}");

                var baseWarranties    = node["data"]?["baseWarranties"]?.ToObject<JArray>()    ?? new JArray();
                var upgradeWarranties = node["data"]?["upgradeWarranties"]?.ToObject<JArray>() ?? new JArray();
                var all = baseWarranties.Concat(upgradeWarranties).ToList();

                var startDates = all
                    .Select(n => n?["startDate"]?.ToString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => DateTime.Parse(s!)).ToList();

                var endDates = all
                    .Select(n => n?["endDate"]?.ToString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => DateTime.Parse(s!)).ToList();

                string startText = startDates.Any() ? startDates.Min().ToString("M/d/yyyy") : "—";
                string endText   = endDates.Any()   ? endDates.Max().ToString("M/d/yyyy")   : "—";

                try
                {
                    var productJson = await client.GetStringAsync(
                        string.Format(ProductsUrl, _serialNumber));
                    var productNode = JArray.Parse(productJson);
                    var id = productNode.FirstOrDefault()?["Id"]?.ToString();
                    if (id != null)
                        _warrantyLink = new Uri($"https://pcsupport.lenovo.com/products/{id}");
                }
                catch { }

                Dispatcher.Invoke(() =>
                {
                    txtWarrantyStart.Text = startText;
                    txtWarrantyEnd.Text   = endText;
                });
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"LoadWarranty: {ex.Message}");
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
