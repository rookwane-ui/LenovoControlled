using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace LenovoController.Features
{
    public class CameraFeature
    {
        private const string KeyPath =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint Msg, IntPtr wParam, string lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        private static void BroadcastSettingChange()
        {
            SendMessageTimeout(
                HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero,
                "ConsentStore", SMTO_ABORTIFHUNG, 5000, out _);
        }

        private static void RestartCamSvc()
        {
            try
            {
                using var sc = new ServiceController("CamSvc");
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                }
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
            }
            catch { /* service may not exist on all systems */ }
        }

        public bool IsSupported()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        // Reads from HKCU parent key
        public bool GetState()
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath)
                ?? throw new Exception("Camera registry key not found in HKCU.");

            var value = key.GetValue("Value") as string;
            return string.Equals(value, "Allow", StringComparison.OrdinalIgnoreCase);
        }

        public void SetState(bool enabled)
        {
            string value = enabled ? "Allow" : "Deny";

            // Write HKCU parent + all per-app subkeys
            using (var parent = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                parent.SetValue("Value", value, RegistryValueKind.String);

                foreach (var subkeyName in parent.GetSubKeyNames())
                {
                    using var sub = parent.OpenSubKey(subkeyName, writable: true);
                    if (sub?.GetValue("Value") != null)
                        sub.SetValue("Value", value, RegistryValueKind.String);
                }
            }

            // Restart CamSvc to enforce the change immediately
            RestartCamSvc();

            // Broadcast to notify running apps
            BroadcastSettingChange();
        }
    }
}
