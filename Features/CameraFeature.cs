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

        // Reads from HKLM parent key as the authoritative state
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

            // Write HKLM parent + all subkeys (requires admin)
            SetRegistryHive(Registry.LocalMachine, value);

            // Write HKCU parent + all subkeys
            SetRegistryHive(Registry.CurrentUser, value);

            // Notify all running apps immediately
            BroadcastSettingChange();
        }

        private static void SetRegistryHive(RegistryKey hive, string value)
        {
            using var parent = hive.OpenSubKey(KeyPath, writable: true)
                ?? hive.CreateSubKey(KeyPath);

            // Write to parent key
            parent.SetValue("Value", value, RegistryValueKind.String);

            // Write to every per-app subkey (e.g. Microsoft.WindowsCamera_xxx, NonPackaged)
            foreach (var subkeyName in parent.GetSubKeyNames())
            {
                using var sub = parent.OpenSubKey(subkeyName, writable: true);
                if (sub?.GetValue("Value") != null)
                    sub.SetValue("Value", value, RegistryValueKind.String);
            }
        }
    }
}
