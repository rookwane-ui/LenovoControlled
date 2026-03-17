using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace LenovoController.Features
{
    public class MicrophoneFeature
    {
        // Group Policy key — enforced system-wide, overrides ConsentStore
        // LetAppsAccessMicrophone: 0 = user controlled, 1 = force allow, 2 = force deny
        private const string PolicyKeyPath =
            @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy";

        private const string PolicyValueName = "LetAppsAccessMicrophone";

        public bool IsSupported() => true;

        // true = mic ON, false = mic OFF
        public bool GetState()
        {
            using var key = Registry.LocalMachine.OpenSubKey(PolicyKeyPath);
            if (key == null) return true; // key absent = user controlled = allow

            var val = key.GetValue(PolicyValueName);
            if (val == null) return true; // value absent = allow

            return (int)val != 2; // 2 = force deny
        }

        public void SetState(bool enabled)
        {
            if (enabled)
            {
                // Remove policy — restores user control (allow)
                using var key = Registry.LocalMachine.OpenSubKey(PolicyKeyPath, writable: true);
                key?.DeleteValue(PolicyValueName, throwOnMissingValue: false);
            }
            else
            {
                // Force deny
                using var key = Registry.LocalMachine.CreateSubKey(PolicyKeyPath, writable: true);
                key.SetValue(PolicyValueName, 2, RegistryValueKind.DWord);
            }

            RefreshPolicy();
        }

        private static void RefreshPolicy()
        {
            try
            {
                var gp = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "gpupdate.exe",
                        Arguments = "/force /target:computer /wait:0",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };
                gp.Start();
            }
            catch { /* non-critical */ }
        }
    }
}
