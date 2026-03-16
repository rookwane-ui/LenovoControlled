using System.Diagnostics;
using System.Threading.Tasks;

namespace LenovoController.Features
{
    public class MicrophoneFeature
    {
        public bool GetState()
        {
            return true; // optional later
        }

        public async Task SetStateAsync(bool enabled)
        {
            string command = enabled
                ? "Enable-PnpDevice -Class AudioEndpoint -FriendlyName '*Microphone*' -Confirm:$false"
                : "Disable-PnpDevice -Class AudioEndpoint -FriendlyName '*Microphone*' -Confirm:$false";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = command,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            await process.WaitForExitAsync();
        }

        // 🔥 ADD THIS
        public void SetState(bool enabled)
        {
            SetStateAsync(enabled).GetAwaiter().GetResult();
        }
    }
}
