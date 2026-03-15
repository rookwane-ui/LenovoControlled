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
    /// Controls Windows 11 Power Mode using PowerSetActiveOverlayScheme /
    /// PowerGetEffectiveOverlayScheme — the exact same API that Windows
    /// Settings uses internally. Works on all Windows 11 builds regardless
    /// of whether the SUB_ENERGYSAVER subgroup is provisioned.
    ///
    /// Note: overlay schemes apply globally (not separately per AC/DC).
    /// Windows Settings also shows a single value — the AC/DC split shown
    /// in our UI reads/writes the same overlay for both states.
    /// </summary>
    public class PowerModeFeature
    {
        // ── Overlay Scheme GUIDs ─────────────────────────────────────────────
        private static readonly Guid GuidEfficiency  = new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");
        private static readonly Guid GuidBalanced    = new Guid("00000000-0000-0000-0000-000000000000");
        private static readonly Guid GuidPerformance = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");

        // ── P/Invoke ─────────────────────────────────────────────────────────
        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveOverlayScheme(Guid overlaySchemeGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerGetEffectiveOverlayScheme(out Guid effectiveOverlayGuid);

        // ── Read ─────────────────────────────────────────────────────────────
        private PowerModeState GetState()
        {
            try
            {
                uint r = PowerGetEffectiveOverlayScheme(out Guid current);
                if (r != 0)
                {
                    Trace.TraceWarning($"PowerGetEffectiveOverlayScheme failed: {r}");
                    return PowerModeState.Balance;
                }

                if (current == GuidEfficiency)  return PowerModeState.Quiet;
                if (current == GuidPerformance) return PowerModeState.Performance;
                return PowerModeState.Balance;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"PowerModeFeature.GetState: {ex.Message}");
                return PowerModeState.Balance;
            }
        }

        // ── Write ────────────────────────────────────────────────────────────
        private void SetState(PowerModeState state)
        {
            try
            {
                Guid guid = state == PowerModeState.Quiet       ? GuidEfficiency
                          : state == PowerModeState.Performance ? GuidPerformance
                          : GuidBalanced;

                uint r = PowerSetActiveOverlayScheme(guid);
                if (r != 0)
                    Trace.TraceWarning($"PowerSetActiveOverlayScheme failed: {r} for {state}");
                else
                    Trace.TraceInformation($"Power mode set to {state} ({guid})");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"PowerModeFeature.SetState: {ex.Message}");
                throw;
            }
        }

        // ── AC / DC wrappers (overlay is global but UI shows both) ───────────
        // The overlay scheme has no AC/DC split — it's one value.
        // We expose AC/DC methods so MainWindow can treat them independently
        // in the UI; setting one sets both since the API is global.
        public PowerModeState GetAcState() => GetState();
        public PowerModeState GetDcState() => GetState();
        public void SetAcState(PowerModeState state) => SetState(state);
        public void SetDcState(PowerModeState state) => SetState(state);
    }
}
