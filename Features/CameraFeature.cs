using Microsoft.Win32;
using System;

namespace LenovoController.Features
{
    public class CameraFeature
    {
        private const string HklmKey =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";

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
        }
    }
}
