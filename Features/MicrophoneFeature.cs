using Microsoft.Win32;

namespace LenovoController.Features
{
    public class MicrophoneFeature
    {
        private const string MicKey =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

        public bool GetState()
        {
            using var key = Registry.CurrentUser.OpenSubKey(MicKey);

            if (key == null)
                return true;

            string value = key.GetValue("Value")?.ToString();
            return value != "Deny";
        }

        public void SetState(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(MicKey, true);

            key?.SetValue("Value", enabled ? "Allow" : "Deny", RegistryValueKind.String);
        }
    }
}
