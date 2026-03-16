using NAudio.CoreAudioApi;
using System.Linq;
using System.Threading.Tasks;

namespace LenovoController.Features
{
    public class MicrophoneFeature
    {
        private readonly MMDeviceEnumerator _enumerator = new MMDeviceEnumerator();

        private System.Collections.Generic.IEnumerable<AudioEndpointVolume> GetVolumes() =>
            _enumerator
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .Select(d => d.AudioEndpointVolume);

        public bool IsSupported()
        {
            try { return GetVolumes().Any(); }
            catch { return false; }
        }

        public bool GetState()
        {
            // true = mic ON (not muted), false = mic OFF (muted)
            return !GetVolumes().Aggregate(true, (current, v) => current && v.Mute);
        }

        public void SetState(bool enabled)
        {
            foreach (var v in GetVolumes())
                v.Mute = !enabled;
        }

        public Task SetStateAsync(bool enabled)
        {
            SetState(enabled);
            return Task.CompletedTask;
        }
    }
}
