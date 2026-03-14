using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LenovoController.Features
{
    public enum PowerModeState
    {
        Quiet       = 0,   // Best power efficiency
        Balance     = 1,   // Balanced
        Performance = 2    // Best performance
    }

    public class PowerModeFeature : IFeature<PowerModeState>
    {
        // ── GUIDs ────────────────────────────────────────────────────────────────
        // Active scheme — Balanced (the only scheme on modern Win11)
        private static readonly Guid SchemeBalanced = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");

        // SUB_ENERGYSAVER subgroup
        private static readonly Guid SubEnergySaver = new Guid("de830923-a562-41af-a086-e3a2c6bad2da");

        // Power mode overlay setting within that subgroup
        private static readonly Guid SettingPowerMode = new Guid("bbdc3814-18e9-4463-8a55-d197327c45c0");

        // Windows 11 power mode index values
        private const uint IndexEfficiency  = 0;  // Best power efficiency
        private const uint IndexBalanced    = 1;  // Balanced
        private const uint IndexPerformance = 3;  // Best performance (skips 2)

        // ── P/Invoke ─────────────────────────────────────────────────────────────
        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(
            IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveScheme(
            IntPtr RootPowerKey, ref Guid SchemeGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadACValueIndex(
            IntPtr RootPowerKey, ref Guid SchemeGuid,
            ref Guid SubGroupGuid, ref Guid PowerSettingGuid,
            out uint AcValueIndex);

        [DllImport("powrprof.dll")]
        private static extern uint PowerWriteACValueIndex(
            IntPtr RootPowerKey, ref Guid SchemeGuid,
            ref Guid SubGroupGuid, ref Guid PowerSettingGuid,
            uint AcValueIndex);

        [DllImport("powrprof.dll")]
        private static extern uint PowerWriteDCValueIndex(
            IntPtr RootPowerKey, ref Guid SchemeGuid,
            ref Guid SubGroupGuid, ref Guid PowerSettingGuid,
            uint DcValueIndex);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        // ── IFeature ─────────────────────────────────────────────────────────────
        public PowerModeState GetState()
        {
            try
            {
                var scheme  = GetActiveSchemeGuid();
                var sub     = SubEnergySaver;
                var setting = SettingPowerMode;

                uint result = PowerReadACValueIndex(
                    IntPtr.Zero, ref scheme, ref sub, ref setting, out uint index);

                if (result != 0)
                {
                    // Setting not available on this machine (e.g. Windows 10)
                    // Return Balance as a safe default so buttons stay enabled
                    Trace.TraceWarning($"PowerReadACValueIndex failed: {result}. Defaulting to Balance.");
                    return PowerModeState.Balance;
                }

                return IndexToState(index);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"PowerModeFeature.GetState failed: {ex.Message}. Defaulting to Balance.");
                return PowerModeState.Balance;
            }
        }

        public void SetState(PowerModeState state)
        {
            var scheme  = GetActiveSchemeGuid();
            var sub     = SubEnergySaver;
            var setting = SettingPowerMode;
            uint index  = StateToIndex(state);

            // Write both AC (plugged in) and DC (on battery) — mirrors what
            // Windows Settings does when you change Power Mode
            uint r1 = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, index);
            uint r2 = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, index);

            if (r1 != 0) Trace.TraceWarning($"PowerWriteACValueIndex failed: {r1}");
            if (r2 != 0) Trace.TraceWarning($"PowerWriteDCValueIndex failed: {r2}");

            // Must call PowerSetActiveScheme to apply the written values
            uint r3 = PowerSetActiveScheme(IntPtr.Zero, ref scheme);
            if (r3 != 0) Trace.TraceWarning($"PowerSetActiveScheme failed: {r3}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        private static Guid GetActiveSchemeGuid()
        {
            uint result = PowerGetActiveScheme(IntPtr.Zero, out IntPtr ptr);
            if (result != 0)
            {
                // Fall back to Balanced if we can't read the active scheme
                Trace.TraceWarning($"PowerGetActiveScheme failed: {result}, using Balanced");
                return SchemeBalanced;
            }

            try
            {
                return (Guid)Marshal.PtrToStructure(ptr, typeof(Guid));
            }
            finally
            {
                LocalFree(ptr);
            }
        }

        private static PowerModeState IndexToState(uint index)
        {
            switch (index)
            {
                case IndexEfficiency:  return PowerModeState.Quiet;
                case IndexPerformance: return PowerModeState.Performance;
                default:               return PowerModeState.Balance;
            }
        }

        private static uint StateToIndex(PowerModeState state)
        {
            switch (state)
            {
                case PowerModeState.Quiet:       return IndexEfficiency;
                case PowerModeState.Performance: return IndexPerformance;
                default:                         return IndexBalanced;
            }
        }
    }
}
