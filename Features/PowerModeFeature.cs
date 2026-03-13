using System;
using System.Collections.Generic;
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
        // ── Windows 11 power plan GUIDs ──────────────────────────────────────────
        // These are the three built-in plans present on every Windows 11 machine.
        // Lenovo's Vantage uses the same GUIDs to sync fan mode with power plan.
        private static readonly Guid GuidPowerSaver    = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");
        private static readonly Guid GuidBalanced      = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        private static readonly Guid GuidHighPerf      = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        private static readonly Guid GuidUltimatePerf  = new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61");

        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveScheme(IntPtr RootPowerKey, ref Guid SchemeGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        public PowerModeFeature() : base("SmartFanMode", 1) { }

        public new void SetState(PowerModeState state)
        {
            // 1. Set Lenovo fan/thermal mode via WMI (existing behaviour)
            base.SetState(state);

            // 2. Sync Windows power plan to match
            try
            {
                var guid = GetPowerPlanGuid(state);
                var result = PowerSetActiveScheme(IntPtr.Zero, ref guid);
                if (result != 0)
                    Trace.TraceWarning($"PowerSetActiveScheme returned {result} for state {state}");
            }
            catch (Exception ex)
            {
                // Power plan switch is best-effort — don't crash if it fails
                Trace.TraceWarning($"Could not set Windows power plan for {state}: {ex.Message}");
            }
        }

        public new PowerModeState GetState()
        {
            // Read from WMI as before — it's the authoritative source for fan mode
            return base.GetState();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        private static Guid GetPowerPlanGuid(PowerModeState state)
        {
            switch (state)
            {
                case PowerModeState.Quiet:
                    return GuidPowerSaver;

                case PowerModeState.Balance:
                    return GuidBalanced;

                case PowerModeState.Performance:
                    // Prefer Ultimate Performance if available on this machine,
                    // fall back to High Performance
                    return IsPlanAvailable(GuidUltimatePerf) ? GuidUltimatePerf : GuidHighPerf;

                default:
                    return GuidBalanced;
            }
        }

        private static bool IsPlanAvailable(Guid guid)
        {
            // PowerGetActiveScheme just proves powrprof is present;
            // we enumerate plans to check if Ultimate Performance exists
            try
            {
                uint index = 0;
                while (true)
                {
                    IntPtr schemeGuidPtr;
                    uint bufSize = (uint)Marshal.SizeOf(typeof(Guid));
                    uint result = PowerEnumerate(
                        IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                        POWER_DATA_ACCESSOR.ACCESS_SCHEME,
                        index, out schemeGuidPtr, ref bufSize);

                    if (result != 0) break;

                    var found = (Guid)Marshal.PtrToStructure(schemeGuidPtr, typeof(Guid));
                    LocalFree(schemeGuidPtr);

                    if (found == guid) return true;
                    index++;
                }
            }
            catch
            {
                // Ignore — just fall back to High Performance
            }
            return false;
        }

        private enum POWER_DATA_ACCESSOR : uint
        {
            ACCESS_SCHEME = 16
        }

        [DllImport("powrprof.dll")]
        private static extern uint PowerEnumerate(
            IntPtr RootPowerKey,
            IntPtr SchemeGuid,
            IntPtr SubGroupOfPowerSettingGuid,
            POWER_DATA_ACCESSOR AccessFlags,
            uint Index,
            out IntPtr Buffer,
            ref uint BufferSize);
    }
}
