using Microsoft.Win32;
using System;

namespace LenovoController.Features
{
    public class CameraFeature
    {
        private const string RegistryKey =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";

        public bool IsSupported()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
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
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey)
                ?? throw new Exception("Camera registry key not found.");

            var value = key.GetValue("Value") as string;
            return string.Equals(value, "Allow", StringComparison.OrdinalIgnoreCase);
        }

        public void SetState(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true)
                ?? throw new Exception("Camera registry key not found.");

            key.SetValue("Value", enabled ? "Allow" : "Deny", RegistryValueKind.String);
        }
    }
}
