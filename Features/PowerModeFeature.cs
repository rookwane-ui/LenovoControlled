using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LenovoController.Features
{
    public enum PowerModeState
    {
        Quiet       = 0,   // Efficiency  / Battery life
        Balance     = 1,   // Balanced
        Performance = 2    // Performance
    }

    /// <summary>
    /// Controls power mode by writing to the settings that actually exist
    /// in this machine's power scheme:
    ///
    ///   1. Intel Graphics Power Plan (SUB_44F3BECA / 3619C3F2)
    ///      0 = Max Battery Life, 1 = Balanced, 2 = Max Performance
    ///
    ///   2. Processor max state (SUB_PROCESSOR / PROCTHROTTLEMAX)
    ///      Quiet=60%, Balance=100%, Performance=100%
    ///
    ///   3. Processor min state (SUB_PROCESSOR / PROCTHROTTLEMIN)
    ///      Quiet=0%, Balance=5%, Performance=5%
    ///
    ///   4. Wireless adapter power saving (SUB_WIRELESSADAPTER)
    ///      Quiet=3 (Max saving), Balance=1 (Low saving), Performance=0 (Max perf)
    ///
    /// GetState reads the Intel Graphics setting as the authoritative source.
    /// </summary>
    public class PowerModeFeature
    {
        // ── Subgroup / Setting GUIDs (confirmed present on this machine) ─────
        // Intel Graphics
        private static readonly Guid SubIntelGraphics   = new Guid("44f3beca-a7c0-460e-9df2-bb8b99e0cba6");
        private static readonly Guid SettingIntelGfx    = new Guid("3619c3f2-afb2-4afc-b0e9-e7fef372de36");

        // Processor
        private static readonly Guid SubProcessor       = new Guid("54533251-82be-4824-96c1-47b60b740d00");
        private static readonly Guid SettingProcMax     = new Guid("bc5038f7-23e0-4960-96da-33abaf5935ec");
        private static readonly Guid SettingProcMin     = new Guid("893dee8e-2bef-41e0-89c6-b55d0929964c");

        // Wireless adapter
        private static readonly Guid SubWireless        = new Guid("19cbb8fa-5279-450e-9fac-8a3d5fedd0c1");
        private static readonly Guid SettingWireless    = new Guid("12bbebe6-58d6-4636-95bb-3217ef867c1a");

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
                var scheme = GetScheme();
                var sub    = SubIntelGraphics;
                var s      = SettingIntelGfx;
                if (PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref s, out uint idx) == 0)
                    return (PowerModeState)Math.Min((int)idx, 2);
            }
            catch (Exception ex) { Trace.TraceWarning($"GetAcState: {ex.Message}"); }
            return PowerModeState.Balance;
        }

        public void SetAcState(PowerModeState state)
        {
            var scheme = GetScheme();
            WriteAC(scheme, SubIntelGraphics, SettingIntelGfx, (uint)state);

            // Processor max: Quiet=60%, Balance/Performance=100%
            uint procMax = state == PowerModeState.Quiet ? 60u : 100u;
            WriteAC(scheme, SubProcessor, SettingProcMax, procMax);

            // Processor min: Quiet=0%, else 5%
            uint procMin = state == PowerModeState.Quiet ? 0u : 5u;
            WriteAC(scheme, SubProcessor, SettingProcMin, procMin);

            // Wireless: Quiet=MaxSaving(3), Balance=LowSaving(1), Perf=MaxPerf(0)
            uint wifi = state == PowerModeState.Quiet ? 3u
                      : state == PowerModeState.Balance ? 1u : 0u;
            WriteAC(scheme, SubWireless, SettingWireless, wifi);

            Apply(ref scheme);
        }

        // ── DC (On battery) ───────────────────────────────────────────────────
        public PowerModeState GetDcState()
        {
            try
            {
                var scheme = GetScheme();
                var sub    = SubIntelGraphics;
                var s      = SettingIntelGfx;
                if (PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref s, out uint idx) == 0)
                    return (PowerModeState)Math.Min((int)idx, 2);
            }
            catch (Exception ex) { Trace.TraceWarning($"GetDcState: {ex.Message}"); }
            return PowerModeState.Balance;
        }

        public void SetDcState(PowerModeState state)
        {
            var scheme = GetScheme();
            WriteDC(scheme, SubIntelGraphics, SettingIntelGfx, (uint)state);

            uint procMax = state == PowerModeState.Quiet ? 60u : 100u;
            WriteDC(scheme, SubProcessor, SettingProcMax, procMax);

            uint procMin = state == PowerModeState.Quiet ? 0u : 5u;
            WriteDC(scheme, SubProcessor, SettingProcMin, procMin);

            uint wifi = state == PowerModeState.Quiet ? 3u
                      : state == PowerModeState.Balance ? 2u : 0u;
            WriteDC(scheme, SubWireless, SettingWireless, wifi);

            Apply(ref scheme);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static Guid GetScheme()
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out IntPtr ptr) != 0)
                return new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
            try   { return (Guid)Marshal.PtrToStructure(ptr, typeof(Guid)); }
            finally { LocalFree(ptr); }
        }

        private static void Apply(ref Guid scheme)
        {
            uint r = PowerSetActiveScheme(IntPtr.Zero, ref scheme);
            if (r != 0) Trace.TraceWarning($"PowerSetActiveScheme: {r}");
        }

        private static void WriteAC(Guid scheme, Guid sub, Guid setting, uint val)
        {
            uint r = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, val);
            if (r != 0) Trace.TraceWarning($"WriteAC {setting}: {r}");
        }

        private static void WriteDC(Guid scheme, Guid sub, Guid setting, uint val)
        {
            uint r = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, val);
            if (r != 0) Trace.TraceWarning($"WriteDC {setting}: {r}");
        }
    }
}
