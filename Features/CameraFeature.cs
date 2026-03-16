using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

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

        // Mirrors exactly what Windows Settings does:
        // write HKCU parent + all per-app subkeys, then broadcast
        public void SetState(bool enabled)
        {
            string value = enabled ? "Allow" : "Deny";

            using var parent = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(KeyPath);

            // Write parent key
            parent.SetValue("Value", value, RegistryValueKind.String);

            // Write every per-app subkey (e.g. Microsoft.WindowsCamera_xxx, NonPackaged)
            foreach (var subkeyName in parent.GetSubKeyNames())
            {
                using var sub = parent.OpenSubKey(subkeyName, writable: true);
                if (sub?.GetValue("Value") != null)
                    sub.SetValue("Value", value, RegistryValueKind.String);
            }

            // Notify running apps immediately
            BroadcastSettingChange();
        }
    }
}
