using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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
    /// PowerGetEffectiveOverlayScheme — the exact same API Windows Settings uses.
    ///
    /// The overlay is a single global value. However Battery Saver forces
    /// the effective overlay to BestPowerEfficiency when on battery, which
    /// is why the DC row always appeared as Quiet. We handle this by:
    ///   - GetDcState: if on battery and Battery Saver is on, return Quiet
    ///   - SetDcState: if setting non-Quiet, disable Battery Saver first
    /// </summary>
    public class PowerModeFeature
    {
        // ── Overlay Scheme GUIDs ─────────────────────────────────────────────
        private static readonly Guid GuidEfficiency  = new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");
        private static readonly Guid GuidBalanced    = new Guid("00000000-0000-0000-0000-000000000000");
        private static readonly Guid GuidPerformance = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");

        // Battery Saver registry key (HKCU)
        private const string BatterySaverKey   = @"Control Panel\Power";
        private const string BatterySaverValue = "BatterySaverStatus";

        // ── P/Invoke ─────────────────────────────────────────────────────────
        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveOverlayScheme(Guid overlaySchemeGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerGetEffectiveOverlayScheme(out Guid effectiveOverlayGuid);

        // ── AC (Plugged in) ───────────────────────────────────────────────────
        public PowerModeState GetAcState()
        {
            // When on AC, the overlay reflects the true mode
            // When on DC, the overlay may be overridden by Battery Saver
            // For AC we always read the effective overlay directly
            try
            {
                uint r = PowerGetEffectiveOverlayScheme(out Guid current);
                if (r != 0) return PowerModeState.Balance;
                return GuidToState(current);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"GetAcState: {ex.Message}");
                return PowerModeState.Balance;
            }
        }

        public void SetAcState(PowerModeState state)
        {
            SetOverlay(state);
        }

        // ── DC (On battery) ───────────────────────────────────────────────────
        public PowerModeState GetDcState()
        {
            try
            {
                // If Battery Saver is active, effective mode is always Quiet
                if (IsBatterySaverOn())
                    return PowerModeState.Quiet;

                uint r = PowerGetEffectiveOverlayScheme(out Guid current);
                if (r != 0) return PowerModeState.Balance;
                return GuidToState(current);
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
                // If user wants anything other than Quiet, Battery Saver must be off
                // because it would override our setting back to BestPowerEfficiency
                if (state != PowerModeState.Quiet && IsBatterySaverOn())
                {
                    DisableBatterySaver();
                    Trace.TraceInformation("Battery Saver disabled to allow power mode change.");
                }

                SetOverlay(state);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"SetDcState: {ex.Message}");
                throw;
            }
        }

        // ── Battery Saver ────────────────────────────────────────────────────
        private static bool IsBatterySaverOn()
        {
            // Check PowerLineStatus — Battery Saver only applies on DC
            bool isOnBattery = SystemInformation.PowerStatus.PowerLineStatus
                               == PowerLineStatus.Offline;
            if (!isOnBattery) return false;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(BatterySaverKey, false);
                if (key == null) return false;
                var val = key.GetValue(BatterySaverValue);
                // BatterySaverStatus = 1 means Battery Saver is ON
                return val != null && Convert.ToInt32(val) == 1;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"IsBatterySaverOn: {ex.Message}");
                return false;
            }
        }

        private static void DisableBatterySaver()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(BatterySaverKey, true);
                key?.SetValue(BatterySaverValue, 0, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"DisableBatterySaver: {ex.Message}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static void SetOverlay(PowerModeState state)
        {
            Guid guid = state == PowerModeState.Quiet       ? GuidEfficiency
                      : state == PowerModeState.Performance ? GuidPerformance
                      : GuidBalanced;

            uint r = PowerSetActiveOverlayScheme(guid);
            if (r != 0)
                Trace.TraceWarning($"PowerSetActiveOverlayScheme failed: {r} for {state}");
            else
                Trace.TraceInformation($"Power overlay set to {state} ({guid})");
        }

        private static PowerModeState GuidToState(Guid guid)
        {
            if (guid == GuidEfficiency)  return PowerModeState.Quiet;
            if (guid == GuidPerformance) return PowerModeState.Performance;
            return PowerModeState.Balance;
        }
    }
}
