using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace LenovoController.Features
{
    public class CameraFeature
    {
        // Group Policy key — enforced system-wide, overrides ConsentStore
        // LetAppsAccessCamera: 0 = user controlled, 1 = force allow, 2 = force deny
        private const string PolicyKeyPath =
            @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy";

        private const string PolicyValueName = "LetAppsAccessCamera";

        public bool IsSupported()
        {
            try
            {
                // Always supported — we create the key if it doesn't exist
                return true;
            }
            catch
            {
                return false;
            }
        }

        // true = camera ON, false = camera OFF
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
                // Remove the policy entirely — restores user control (allow)
                using var key = Registry.LocalMachine.OpenSubKey(PolicyKeyPath, writable: true);
                key?.DeleteValue(PolicyValueName, throwOnMissingValue: false);
            }
            else
            {
                // Create key if needed, set force deny
                using var key = Registry.LocalMachine.CreateSubKey(PolicyKeyPath, writable: true);
                key.SetValue(PolicyValueName, 2, RegistryValueKind.DWord);
            }

            // Apply group policy immediately without full gpupdate
            RefreshPolicy();
        }

        private static void RefreshPolicy()
        {
            try
            {
                // RefreshPolicy(true) = machine policy, faster than gpupdate /force
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        Arguments = "user32.dll,UpdatePerUserSystemParameters 1, True",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };
                process.Start();
                process.WaitForExit();

                // Also run gpupdate for machine policy
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
                // Don't wait — gpupdate can be slow, registry change is already written
            }
            catch { /* non-critical */ }
        }
    }
}
