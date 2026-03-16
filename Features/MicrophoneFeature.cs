using Microsoft.Win32;
using System.Diagnostics;

namespace LenovoController.Features
{
    public class MicrophoneFeature
    {
        private const string UserKey =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

        private const string MachineKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

        public bool GetState()
        {
            using var key = Registry.CurrentUser.OpenSubKey(UserKey);

            if (key == null)
                return true;

            string value = key.GetValue("Value")?.ToString();

            return value != "Deny";
        }

        public void SetState(bool enabled)
        {
            string state = enabled ? "Allow" : "Deny";

            // HKCU (user level)
            using (var key = Registry.CurrentUser.CreateSubKey(UserKey))
            {
                key?.SetValue("Value", state, RegistryValueKind.String);
            }

            // HKLM (system level) – requires admin
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(MachineKey);
                key?.SetValue("Value", state, RegistryValueKind.String);
            }
            catch
            {
                // Ignore if not admin
            }

            // Notify Windows
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "Restart-Service camsvc -Force",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }
    }
}
