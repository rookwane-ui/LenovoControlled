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
                using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        // Reads from HKLM as the authoritative state
        public bool GetState()
        {
            using var key = Registry.LocalMachine.OpenSubKey(KeyPath)
                ?? throw new Exception("Camera registry key not found in HKLM.");

            var value = key.GetValue("Value") as string;
            return string.Equals(value, "Allow", StringComparison.OrdinalIgnoreCase);
        }

        public void SetState(bool enabled)
        {
            string value = enabled ? "Allow" : "Deny";

            // Write HKLM — master switch (requires admin)
            using (var hklm = Registry.LocalMachine.OpenSubKey(KeyPath, writable: true)
                ?? throw new Exception("Camera registry key not found in HKLM."))
            {
                hklm.SetValue("Value", value, RegistryValueKind.String);
            }

            // Write HKCU — must match HKLM or it overrides it
            using (var hkcu = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                hkcu.SetValue("Value", value, RegistryValueKind.String);
            }

            // Notify all running apps immediately
            BroadcastSettingChange();
        }
    }
}
