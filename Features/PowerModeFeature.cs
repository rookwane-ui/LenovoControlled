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

    /// <summary>
    /// Reads and writes the Windows 11 Power Mode overlay separately for
    /// AC (plugged in) and DC (on battery) — exactly matching the two
    /// dropdowns in Settings > System > Power > Power mode.
    /// </summary>
    public class PowerModeFeature
    {
        // ── GUIDs ────────────────────────────────────────────────────────────
        private static readonly Guid SubEnergySaver  = new Guid("de830923-a562-41af-a086-e3a2c6bad2da");
        private static readonly Guid SettingPowerMode = new Guid("bbdc3814-18e9-4463-8a55-d197327c45c0");

        // Index values used by Windows 11
        private const uint IndexEfficiency  = 0;
        private const uint IndexBalanced    = 1;
        private const uint IndexPerformance = 3;  // note: 2 is unused

        // ── P/Invoke ─────────────────────────────────────────────────────────
        [DllImport("powrprof.dll")] private static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr guid);
        [DllImport("powrprof.dll")] private static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
        [DllImport("powrprof.dll")] private static extern uint PowerReadACValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, out uint val);
        [DllImport("powrprof.dll")] private static extern uint PowerReadDCValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, out uint val);
        [DllImport("powrprof.dll")] private static extern uint PowerWriteACValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, uint val);
        [DllImport("powrprof.dll")] private static extern uint PowerWriteDCValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, uint val);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr mem);

        // ── AC (Plugged in) ───────────────────────────────────────────────────
        public PowerModeState GetAcState()
        {
            try
            {
                var scheme = GetScheme(); var sub = SubEnergySaver; var s = SettingPowerMode;
                if (PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref s, out uint idx) != 0)
                    return PowerModeState.Balance;
                return ToState(idx);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"GetAcState: {ex.Message}");
                return PowerModeState.Balance;
            }
        }

        public void SetAcState(PowerModeState state)
        {
            try
            {
                var scheme = GetScheme(); var sub = SubEnergySaver; var s = SettingPowerMode;
                uint idx = ToIndex(state);
                uint r = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref s, idx);
                if (r != 0) Trace.TraceWarning($"PowerWriteACValueIndex: {r}");
                Apply(ref scheme);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"SetAcState: {ex.Message}");
                throw;
            }
        }

        // ── DC (On battery) ───────────────────────────────────────────────────
        public PowerModeState GetDcState()
        {
            try
            {
                var scheme = GetScheme(); var sub = SubEnergySaver; var s = SettingPowerMode;
                if (PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref s, out uint idx) != 0)
                    return PowerModeState.Balance;
                return ToState(idx);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"GetDcState: {ex.Message}");
                return PowerModeState.Balance;
            }
        }

        public void SetDcState(PowerModeState state)
        {
            try
            {
                var scheme = GetScheme(); var sub = SubEnergySaver; var s = SettingPowerMode;
                uint idx = ToIndex(state);
                uint r = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref s, idx);
                if (r != 0) Trace.TraceWarning($"PowerWriteDCValueIndex: {r}");
                Apply(ref scheme);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"SetDcState: {ex.Message}");
                throw;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static Guid GetScheme()
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out IntPtr ptr) != 0)
                return new Guid("381b4222-f694-41f0-9685-ff5bb260df2e"); // Balanced fallback
            try   { return (Guid)Marshal.PtrToStructure(ptr, typeof(Guid)); }
            finally { LocalFree(ptr); }
        }

        private static void Apply(ref Guid scheme)
        {
            uint r = PowerSetActiveScheme(IntPtr.Zero, ref scheme);
            if (r != 0) Trace.TraceWarning($"PowerSetActiveScheme: {r}");
        }

        private static PowerModeState ToState(uint idx)
        {
            if (idx == IndexEfficiency)  return PowerModeState.Quiet;
            if (idx == IndexPerformance) return PowerModeState.Performance;
            return PowerModeState.Balance;
        }

        private static uint ToIndex(PowerModeState s)
        {
            if (s == PowerModeState.Quiet)       return IndexEfficiency;
            if (s == PowerModeState.Performance) return IndexPerformance;
            return IndexBalanced;
        }
    }
}
