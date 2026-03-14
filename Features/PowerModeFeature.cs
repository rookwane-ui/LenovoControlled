using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LenovoController.Features
{
    public enum PowerModeState
    {
        Quiet       = 0,   // Power saver
        Balance     = 1,   // Balanced
        Performance = 2    // High performance / Ultimate performance
    }

    public class PowerModeFeature : IFeature<PowerModeState>
    {
        // ── Built-in Windows power plan GUIDs ────────────────────────────────────
        private static readonly Guid GuidPowerSaver   = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");
        private static readonly Guid GuidBalanced     = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        private static readonly Guid GuidHighPerf     = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        private static readonly Guid GuidUltimatePerf = new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61");

        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveScheme(IntPtr RootPowerKey, ref Guid SchemeGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        public PowerModeState GetState()
        {
            IntPtr ptr;
            uint result = PowerGetActiveScheme(IntPtr.Zero, out ptr);
            if (result != 0)
                throw new InvalidOperationException($"PowerGetActiveScheme failed: {result}");

            try
            {
                var active = (Guid)Marshal.PtrToStructure(ptr, typeof(Guid));

                if (active == GuidPowerSaver)
                    return PowerModeState.Quiet;

                if (active == GuidHighPerf || active == GuidUltimatePerf)
                    return PowerModeState.Performance;

                // Balanced or any custom plan — treat as Balance
                return PowerModeState.Balance;
            }
            finally
            {
                LocalFree(ptr);
            }
        }

        public void SetState(PowerModeState state)
        {
            Guid guid;

            switch (state)
            {
                case PowerModeState.Quiet:
                    guid = GuidPowerSaver;
                    break;

                case PowerModeState.Performance:
                    // Try Ultimate Performance first — only exists on Pro/Enterprise
                    // or if the user has previously activated it
                    guid = GuidUltimatePerf;
                    uint r = PowerSetActiveScheme(IntPtr.Zero, ref guid);
                    if (r == 0)
                    {
                        Trace.TraceInformation("Power plan set to Ultimate Performance.");
                        return;
                    }
                    // Fall back to High Performance
                    guid = GuidHighPerf;
                    break;

                default:
                    guid = GuidBalanced;
                    break;
            }

            uint result = PowerSetActiveScheme(IntPtr.Zero, ref guid);
            if (result != 0)
                Trace.TraceWarning($"PowerSetActiveScheme returned {result} for {state}");
            else
                Trace.TraceInformation($"Power plan set to {state}.");
        }
    }
}
