using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Management;
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

            LoadSystemInfo();
            LoadBatteryInfo();
            LoadWarrantyInfo();
        }

        private void LoadSystemInfo()
        {
            try
            {
                const string key =
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

                using var reg =
                    Registry.LocalMachine.OpenSubKey(key);

                if (reg == null) return;

                txtWinEdition.Text =
                    reg.GetValue("ProductName")?.ToString() ?? "—";

                txtWinVersion.Text =
                    reg.GetValue("DisplayVersion")?.ToString() ?? "—";

                txtWinBuild.Text =
                    reg.GetValue("CurrentBuild")?.ToString() ?? "—";

                txtWinArch.Text =
                    Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";

                var install = reg.GetValue("InstallDate");

                if (install != null &&
                    long.TryParse(install.ToString(), out long seconds))
                {
                    var date =
                        DateTimeOffset.FromUnixTimeSeconds(seconds)
                        .LocalDateTime;

                    txtInstallDate.Text =
                        date.ToString("yyyy-MM-dd");
                }
            }
            catch { }
        }

        private void LoadBatteryInfo()
        {
            try
            {
                ulong design = 0;
                ulong full = 0;
                uint cycles = 0;

                using var s1 =
                    new ManagementObjectSearcher("root\\WMI",
                        "SELECT * FROM BatteryStaticData");

                foreach (ManagementObject obj in s1.Get())
                {
                    design = Convert.ToUInt64(obj["DesignedCapacity"]);
                    break;
                }

                using var s2 =
                    new ManagementObjectSearcher("root\\WMI",
                        "SELECT * FROM BatteryFullChargedCapacity");

                foreach (ManagementObject obj in s2.Get())
                {
                    full = Convert.ToUInt64(obj["FullChargedCapacity"]);
                    break;
                }

                using var s3 =
                    new ManagementObjectSearcher("root\\WMI",
                        "SELECT * FROM BatteryCycleCount");

                foreach (ManagementObject obj in s3.Get())
                {
                    cycles = Convert.ToUInt32(obj["CycleCount"]);
                    break;
                }

                double health =
                    (design > 0 && full > 0)
                    ? (double)full / design * 100
                    : 0;

                txtBatteryHealth.Text =
                    health > 0 ? $"{health:F1}%" : "—";

                txtBatteryDesign.Text =
                    design > 0 ? $"{design / 1000.0:F1} Wh" : "—";

                txtBatteryFull.Text =
                    full > 0 ? $"{full / 1000.0:F1} Wh" : "—";

                txtBatteryCycles.Text =
                    cycles > 0 ? cycles.ToString() : "—";

                batteryHealthBar.Width =
                    Math.Min(80, 80 * health / 100);
            }
            catch { }
        }

        private void LoadWarrantyInfo()
        {
            txtWarrantyStart.Text = "—";
            txtWarrantyEnd.Text = "—";
        }
    }
}
