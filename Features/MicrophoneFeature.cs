using Windows.Media.Capture;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;

public class ModernMicrophoneFeature
{
    public async Task<bool> GetStateAsync()
    {
        // Check microphone permission status
        var accessStatus = await AudioCapturePermissions.RequestMicrophoneAccessAsync();
        return accessStatus == MediaCaptureAccessStatus.Allowed;
    }

    public async Task SetStateAsync(bool enabled)
    {
        // On Windows 11, you can request permission change
        if (enabled)
        {
            // Request microphone access
            var accessStatus = await AudioCapturePermissions.RequestMicrophoneAccessAsync();
            
            if (accessStatus != MediaCaptureAccessStatus.Allowed)
            {
                // Open Windows privacy settings if permission denied
                await Windows.System.Launcher.LaunchUriAsync(
                    new Uri("ms-settings:privacy-microphone")
                );
            }
        }
        else
            {
            // Open Windows privacy settings for manual toggling
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri("ms-settings:privacy-microphone")
            );
        }
    }
}
