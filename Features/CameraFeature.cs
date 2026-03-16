using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

namespace LenovoController.Features
{
    public class CameraFeature
    {
        private const string HklmKey =
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
                using var key = Registry.LocalMachine.OpenSubKey(HklmKey);
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        // true = camera ON (Allow), false = camera OFF (Deny)
        public bool GetState()
        {
            using var key = Registry.LocalMachine.OpenSubKey(HklmKey)
                ?? throw new Exception("Camera registry key not found in HKLM.");

            var value = key.GetValue("Value") as string;
            return string.Equals(value, "Allow", StringComparison.OrdinalIgnoreCase);
        }

        public void SetState(bool enabled)
        {
            using var key = Registry.LocalMachine.OpenSubKey(HklmKey, writable: true)
                ?? throw new Exception("Camera registry key not found in HKLM.");

            key.SetValue("Value", enabled ? "Allow" : "Deny", RegistryValueKind.String);
            BroadcastSettingChange(); // notify running apps immediately, same as Windows Settings
        }
    }
}
