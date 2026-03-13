using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LenovoController.Features
{
    public enum PowerModeState
    {
        Quiet       = 0,
        Balance     = 1,
        Performance = 2
    }

    public class PowerModeFeature : AbstractWmiFeature<PowerModeState>
    {
        // ── Windows built-in power plan GUIDs ────────────────────────────────────
        private static readonly Guid GuidPowerSaver   = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");
        private static readonly Guid GuidBalanced     = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        private static readonly Guid GuidHighPerf     = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        private static readonly Guid GuidUltimatePerf = new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61");

        [DllImport("powrprof.dll", SetLastError = false)]
        private static extern uint PowerSetActiveScheme(IntPtr RootPowerKey, ref Guid SchemeGuid);

        [DllImport("powrprof.dll", SetLastError = false)]
        private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("kernel32.dll", SetLastError = false)]
        private static extern IntPtr LocalFree(IntPtr hMem);

        public PowerModeFeature() : base("SmartFanMode", 1) { }

        // GetState — reads from WMI only, no power plan involvement
        public new PowerModeState GetState()
        {
            return base.GetState();
        }

        // SetState — sets WMI fan mode then syncs Windows power plan
        public new void SetState(PowerModeState state)
        {
            // Step 1: Lenovo WMI thermal/fan mode (original behaviour)
            base.SetState(state);

            // Step 2: Sync Windows power plan — best effort, never throws
            SyncPowerPlan(state);
        }

        private static void SyncPowerPlan(PowerModeState state)
        {
            try
            {
                var guid = state == PowerModeState.Quiet      ? GuidPowerSaver
                         : state == PowerModeState.Balance    ? GuidBalanced
                         : GetHighPerfGuid();

                uint result = PowerSetActiveScheme(IntPtr.Zero, ref guid);
                if (result != 0)
                    Trace.TraceWarning($"PowerSetActiveScheme failed: error {result} for {state}");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Could not sync Windows power plan for {state}: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns Ultimate Performance GUID if the current active scheme is
        /// already Ultimate Performance (meaning it exists on this machine),
        /// otherwise returns High Performance.
        /// </summary>
        private static Guid GetHighPerfGuid()
        {
            try
            {
                IntPtr ptr;
                if (PowerGetActiveScheme(IntPtr.Zero, out ptr) == 0 && ptr != IntPtr.Zero)
                {
                    var active = (Guid)Marshal.PtrToStructure(ptr, typeof(Guid));
                    LocalFree(ptr);
                    if (active == GuidUltimatePerf)
                        return GuidUltimatePerf;
                }
            }
            catch { }

            // Try to activate Ultimate Performance directly — if it doesn't
            // exist on this machine PowerSetActiveScheme will just fail silently
            // and we catch it in SyncPowerPlan anyway
            return GuidUltimatePerf;
        }
    }
}
