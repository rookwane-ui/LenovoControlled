using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace LenovoController
{
    public partial class AboutWindow : Wpf.Ui.Controls.FluentWindow
    {
        public AboutWindow(IntPtr ownerHandle)
        {
            InitializeComponent();
            new WindowInteropHelper(this).Owner = ownerHandle;
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            await LoadDeviceInfoAsync();
            LoadWindowsInfo();
            LoadBatteryInfo();
        }

        private async Task LoadDeviceInfoAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    string manufacturer = "—";
                    string model = "—";
                    string machineType = "—";
                    string serial = "—";
                    string bios = "—";

                    using var searcher =
                        new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystemProduct");

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        manufacturer = obj["Vendor"]?.ToString() ?? "—";
                        model = obj["Name"]?.ToString() ?? "—";
                        machineType = obj["Version"]?.ToString() ?? "—";
                        serial = obj["IdentifyingNumber"]?.ToString() ?? "—";
                        break;
                    }

                    using var biosSearcher =
                        new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");

                    foreach (ManagementObject obj in biosSearcher.Get())
                    {
                        bios = obj["SMBIOSBIOSVersion"]?.ToString() ?? "—";
                        break;
                    }

                    Dispatcher.Invoke(() =>
                    {
                        txtManufacturer.Text = manufacturer;
                        txtModel.Text = model;
                        txtMachineType.Text = machineType;
                        txtSerial.Text = serial;
                        txtBios.Text = bios;
                    });
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning(ex.Message);
                }
            });
        }

        private void LoadWindowsInfo()
        {
            try
            {
                const string key =
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

                using var reg =
                    Registry.LocalMachine.OpenSubKey(key);

                if (reg == null) return;

                string edition =
                    reg.GetValue("ProductName")?.ToString() ?? "—";

                string version =
                    reg.GetValue("DisplayVersion")?.ToString() ?? "—";

                string build =
                    reg.GetValue("CurrentBuild")?.ToString() ?? "0";

                string ubr =
                    reg.GetValue("UBR")?.ToString() ?? "";

                string fullBuild =
                    string.IsNullOrEmpty(ubr)
                        ? build
                        : $"{build}.{ubr}";

                if (int.TryParse(build, out int b) && b >= 22000)
                    edition = edition.Replace("Windows 10", "Windows 11");

                txtWinEdition.Text = edition;
                txtWinVersion.Text = version;
                txtWinBuild.Text = fullBuild;

                txtWinArch.Text =
                    Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";

                var installValue = reg.GetValue("InstallDate");

                if (installValue != null)
                {
                    long seconds = Convert.ToInt64(installValue);
                    DateTime installDate =
                        DateTimeOffset.FromUnixTimeSeconds(seconds)
                        .LocalDateTime;

                    txtInstallDate.Text =
                        installDate.ToString("yyyy-MM-dd");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(ex.Message);
            }
        }

        private void LoadBatteryInfo()
        {
            try
            {
                ulong design = 0;
                ulong full = 0;
                uint cycles = 0;

                using var designSearcher =
                    new ManagementObjectSearcher(
                        "root\\WMI",
                        "SELECT * FROM BatteryStaticData");

                foreach (ManagementObject obj in designSearcher.Get())
                {
                    design = Convert.ToUInt64(obj["DesignedCapacity"]);
                    break;
                }

                using var fullSearcher =
                    new ManagementObjectSearcher(
                        "root\\WMI",
                        "SELECT * FROM BatteryFullChargedCapacity");

                foreach (ManagementObject obj in fullSearcher.Get())
                {
                    full = Convert.ToUInt64(obj["FullChargedCapacity"]);
                    break;
                }

                using var cycleSearcher =
                    new ManagementObjectSearcher(
                        "root\\WMI",
                        "SELECT * FROM BatteryCycleCount");

                foreach (ManagementObject obj in cycleSearcher.Get())
                {
                    cycles = Convert.ToUInt32(obj["CycleCount"]);
                    break;
                }

                double health =
                    design > 0 && full > 0
                    ? (double)full / design * 100
                    : 0;

                txtBatteryHealth.Text =
                    health > 0 ? $"{health:F1}%" : "—";

                txtBatteryDesign.Text =
                    design > 0 ? $"{design / 1000.0:F1} Wh" : "—";

                txtBatteryFull.Text =
                    full > 0 ? $"{full / 1000.0:F1} Wh" : "—";

                txtBatteryCycles.Text =
                    cycles > 0 ? $"{cycles}" : "—";

                batteryHealthBar.Width =
                    Math.Min(80, 80 * health / 100);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(ex.Message);
            }
        }

        private void BtnRefreshWarranty_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Warranty refresh not implemented here.");
        }
    }
}
