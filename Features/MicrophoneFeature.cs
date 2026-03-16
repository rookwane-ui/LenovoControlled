using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LenovoController.Features
{
    public class MicrophoneFeature
    {
        public bool GetState()
        {
            return true; // TODO: implement actual state read
        }

        public async Task SetStateAsync(bool enabled)
        {
            string filter = "$_.FriendlyName -like '*Microphone*' -or $_.FriendlyName -like '*Mic*'";
            string action = enabled ? "Enable-PnpDevice" : "Disable-PnpDevice";
            string command = $"Get-PnpDevice -Class AudioEndpoint | Where-Object {{{filter}}} | {action} -Confirm:$false";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"{command}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                }
            };

            process.Start();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception($"Microphone toggle failed: {error}");
        }

        public void SetState(bool enabled)
        {
            Task.Run(() => SetStateAsync(enabled)).GetAwaiter().GetResult();
        }
    }
}
