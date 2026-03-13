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
        private static readonly Guid GuidPowerSaver   = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");
        private static readonly Guid GuidBalanced     = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        private static readonly Guid GuidHighPerf     = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        private static readonly Guid GuidUltimatePerf = new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61");

        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveScheme(IntPtr RootPowerKey, ref Guid SchemeGuid);

        public PowerModeFeature() : base("SmartFanMode", 1) { }

        // GetState is inherited unchanged from AbstractWmiFeature — no override needed

        // SetAndSyncState is what MainWindow calls instead of SetState directly
        public void SetAndSyncState(PowerModeState state)
        {
            // Step 1: original WMI fan mode — same as before
            SetState(state);

            // Step 2: sync Windows power plan — completely isolated, never throws
            try
            {
                var guid = state == PowerModeState.Quiet   ? GuidPowerSaver
                         : state == PowerModeState.Balance ? GuidBalanced
                         : GuidHighPerf;

                // Try Ultimate Performance first for Performance mode
                if (state == PowerModeState.Performance)
                {
                    var ult = GuidUltimatePerf;
                    if (PowerSetActiveScheme(IntPtr.Zero, ref ult) == 0)
                        return; // success
                    // Fall through to High Performance if Ultimate not available
                }

                PowerSetActiveScheme(IntPtr.Zero, ref guid);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Power plan sync failed for {state}: {ex.Message}");
            }
        }
    }
}
