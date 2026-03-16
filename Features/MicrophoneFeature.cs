using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoController.Features
{
    public class MicrophoneFeature
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IAudioEndpointVolume ppInterface);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int NotImpl1(); int NotImpl2(); int NotImpl3(); int NotImpl4();
            int NotImpl5(); int NotImpl6(); int NotImpl7(); int NotImpl8();
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        }

        // COM audio APIs require an STA thread — Task.Run uses MTA, so we spin our own
        private static T RunOnSta<T>(Func<T> func)
        {
            T result = default;
            Exception ex = null;

            var thread = new Thread(() =>
            {
                try { result = func(); }
                catch (Exception e) { ex = e; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (ex != null)
                throw ex;

            return result;
        }

        private static void RunOnSta(Action action) =>
            RunOnSta<bool>(() => { action(); return true; });

        private static IAudioEndpointVolume GetMicVolume()
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            enumerator.GetDefaultAudioEndpoint(1, 0, out IMMDevice mic); // 1 = eCapture (microphone)
            var iid = typeof(IAudioEndpointVolume).GUID;
            mic.Activate(ref iid, 1, IntPtr.Zero, out IAudioEndpointVolume vol);
            return vol;
        }

        public bool GetState() => RunOnSta(() =>
        {
            GetMicVolume().GetMute(out bool muted);
            return !muted; // true = mic is ON/enabled
        });

        public void SetState(bool enabled) => RunOnSta(() =>
        {
            var empty = Guid.Empty;
            GetMicVolume().SetMute(!enabled, ref empty); // enabled=true means unmute
        });

        public Task SetStateAsync(bool enabled)
        {
            SetState(enabled);
            return Task.CompletedTask;
        }
    }
}
