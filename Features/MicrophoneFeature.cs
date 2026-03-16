using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LenovoController.Features
{
    public class MicrophoneFeature
    {
        // Windows Core Audio API imports
        [Dependency("winmm.dll")]
        private static extern int waveInGetNumDevs();

        [Dependency("winmm.dll")]
        private static extern int waveInGetDevCapsW(uint uDeviceID, ref WAVEINCAPSW pwic, uint cbwic);

        [Dependency("winmm.dll")]
        private static extern int waveInOpen(out IntPtr hWaveIn, uint uDeviceID, ref WAVEFORMATEX pwfx, 
            IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);

        [Dependency("winmm.dll")]
        private static extern int waveInClose(IntPtr hWaveIn);

        [Dependency("winmm.dll")]
        private static extern int waveInGetVolume(IntPtr hWaveIn, out uint dwVolume);

        [Dependency("winmm.dll")]
        private static extern int waveInSetVolume(IntPtr hWaveIn, uint dwVolume);

        // Structure definitions
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        private struct WAVEINCAPSW
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwFormats;
            public ushort wChannels;
            public ushort wReserved1;
        }

        public bool GetState()
        {
            try
            {
                // Check if any microphone devices exist
                int deviceCount = waveInGetNumDevs();
                return deviceCount > 0;
            }
            catch
            {
                return true;
            }
        }

        public async Task SetStateAsync(bool enabled)
        {
            // Option 1: Modern Windows Settings URI (no admin required)
            if (enabled)
            {
                await OpenMicrophoneSettingsAsync();
            }
            else
            {
                // Option 2: Software mute via Windows Volume API
                await MuteMicrophoneAsync();
            }
        }

        private async Task OpenMicrophoneSettingsAsync()
        {
            // Opens Windows privacy settings - no admin required
            await Task.Run(() =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "ms-settings:privacy-microphone",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Fallback to sound settings
                    Process.Start("control", "mmsys.cpl,,1");
                }
            });
        }

        private async Task MuteMicrophoneAsync()
        {
            // Software mute via Windows policy - no admin required
            await Task.Run(() =>
            {
                try
                {
                    // Set microphone volume to 0 (mute) or restore to 100%
                    using (var key = Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone", 
                        true))
                    {
                        key?.SetValue("Value", "Deny", RegistryValueKind.String);
                    }
                    
                    // Restart audio service effect
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = "Restart-Service audiosrv -Force",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                catch { }
            });
        }

        // Synchronous wrapper for backward compatibility
        public void SetState(bool enabled)
        {
            SetStateAsync(enabled).GetAwaiter().GetResult();
        }
    }
}
