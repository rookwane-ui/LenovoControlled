using Microsoft.Win32;
using System.Diagnostics;
using System.Threading.Tasks;
using System;
using Windows.Media.Capture;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;

namespace LenovoController.Features
{
    public class MicrophoneFeature
    {
        private const string UserKey =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

        private const string MachineKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

        // Original registry-based method (kept for backward compatibility)
        public bool GetState()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UserKey);

                if (key == null)
                    return true;

                string value = key.GetValue("Value")?.ToString();

                return value != "Deny";
            }
            catch
            {
                return true; // Default to true if can't read
            }
        }

        // Original registry-based method (kept for backward compatibility)
        public void SetState(bool enabled)
        {
            string state = enabled ? "Allow" : "Deny";

            // HKCU (user level)
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(UserKey))
                {
                    key?.SetValue("Value", state, RegistryValueKind.String);
                }
            }
            catch { }

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

        // New async method for Windows 11
        public async Task<bool> GetStateAsync()
        {
            try
            {
                // For Windows 10/11, try to use the modern API if available
                if (ApiInformation.IsTypePresent("Windows.Media.Capture.MediaCapture"))
                {
                    var accessStatus = await AudioCapturePermissions.RequestMicrophoneAccessAsync();
                    return accessStatus == MediaCaptureAccessStatus.Allowed;
                }
            }
            catch
            {
                // Fall back to registry method if API fails
            }

            return GetState();
        }

        // New async method for Windows 11
        public async Task SetStateAsync(bool enabled)
        {
            try
            {
                // First try the registry method
                SetState(enabled);

                // On Windows 11, also try the modern approach
                if (enabled)
                {
                    try
                    {
                        var accessStatus = await AudioCapturePermissions.RequestMicrophoneAccessAsync();
                        
                        if (accessStatus != MediaCaptureAccessStatus.Allowed)
                        {
                            // Open Windows privacy settings if permission denied
                            await OpenMicrophoneSettingsAsync();
                        }
                    }
                    catch
                    {
                        await OpenMicrophoneSettingsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting microphone state: {ex.Message}");
            }
        }

        private async Task OpenMicrophoneSettingsAsync()
        {
            try
            {
                // Try Windows 11 settings URI
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:privacy-microphone",
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    // Fallback to Windows 10 settings
                    Process.Start("ms-settings:privacy-microphone");
                }
                catch
                {
                    // Final fallback to classic control panel
                    Process.Start("control", "/name Microsoft.Microphone");
                }
            }
            
            await Task.CompletedTask;
        }
    }
}
