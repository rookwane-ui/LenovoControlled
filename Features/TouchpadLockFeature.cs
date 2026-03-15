using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

namespace LenovoController.Features
{
    public enum TouchpadLockState
    {
        Off,  // touchpad enabled
        On    // touchpad disabled
    }

    /// <summary>
    /// Enables/disables the touchpad by toggling its PnP device.
    /// Works universally — finds the touchpad by matching known name patterns
    /// (ELAN, Synaptics, Alps, HID-compliant, Precision, etc.) rather than
    /// a hardcoded InstanceId, so it works on any laptop.
    /// </summary>
    public class TouchpadLockFeature : IFeature<TouchpadLockState>
    {
        // Ordered by specificity — first match wins
        private static readonly string[] NamePatterns =
        {
            "elan pointing",
            "elan touchpad",
            "synaptics touchpad",
            "synaptics smbus",
            "alps pointing",
            "alps touchpad",
            "i2c hid device",      // generic precision touchpad on many laptops
            "precision touchpad",
            "hid-compliant touchpad",
            "touchpad",
        };

        // Cache the found InstanceId so we don't re-query on every call
        private string _cachedInstanceId;

        // ── Public API ────────────────────────────────────────────────────────

        public TouchpadLockState GetState()
        {
            var id = FindInstanceId();
            if (id == null) throw new InvalidOperationException("Touchpad device not found.");

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Status FROM Win32_PnPEntity WHERE DeviceID = '" +
                    Escape(id) + "'");
                using var results = searcher.Get();

                foreach (ManagementObject obj in results)
                {
                    var status = obj["Status"]?.ToString();
                    // "OK" = enabled, "Unknown" / "Error" = disabled
                    return string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase)
                        ? TouchpadLockState.Off
                        : TouchpadLockState.On;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"TouchpadLockFeature.GetState: {ex.Message}");
                throw;
            }

            return TouchpadLockState.Off;
        }

        public void SetState(TouchpadLockState state)
        {
            var id = FindInstanceId();
            if (id == null) throw new InvalidOperationException("Touchpad device not found.");

            string action = state == TouchpadLockState.On
                ? "Disable-PnpDevice"
                : "Enable-PnpDevice";

            // Escape single quotes in InstanceId for PowerShell
            string safeId = id.Replace("'", "''");
            string script  = $"{action} -InstanceId '{safeId}' -Confirm:$false";

            // Disable/Enable-PnpDevice requires elevation
            var psi = new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NonInteractive -WindowStyle Hidden -Command \"{script}\"",
                UseShellExecute = true,
                Verb            = "runas",
                CreateNoWindow  = true,
            };

            try
            {
                using var proc = Process.Start(psi);
                proc?.WaitForExit(8000);
                Trace.TraceInformation(
                    $"TouchpadLockFeature.SetState({state}) id={id} exit={proc?.ExitCode}");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"TouchpadLockFeature.SetState: {ex.Message}");
                throw;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Finds the PnP InstanceId of the touchpad by querying Win32_PnPEntity
        /// and matching against known name patterns. Result is cached.
        /// </summary>
        private string FindInstanceId()
        {
            if (_cachedInstanceId != null)
                return _cachedInstanceId;

            try
            {
                // Query all Mouse and HIDClass devices
                using var searcher = new ManagementObjectSearcher(
                    "SELECT DeviceID, Name, PNPClass FROM Win32_PnPEntity " +
                    "WHERE PNPClass = 'Mouse' OR PNPClass = 'HIDClass'");
                using var results = searcher.Get();

                // Collect all candidates with their names
                var candidates = new List<(string id, string name)>();
                foreach (ManagementObject obj in results)
                {
                    var id   = obj["DeviceID"]?.ToString();
                    var name = obj["Name"]?.ToString() ?? string.Empty;
                    if (id != null)
                        candidates.Add((id, name.ToLowerInvariant()));
                }

                // Match against patterns in priority order
                foreach (var pattern in NamePatterns)
                {
                    foreach (var (id, name) in candidates)
                    {
                        if (name.Contains(pattern))
                        {
                            Trace.TraceInformation(
                                $"TouchpadLockFeature: found '{name}' -> {id}");
                            _cachedInstanceId = id;
                            return id;
                        }
                    }
                }

                Trace.TraceWarning("TouchpadLockFeature: no touchpad device found.");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"TouchpadLockFeature.FindInstanceId: {ex.Message}");
            }

            return null;
        }

        private static string Escape(string id) => id.Replace(@"\", @"\\");
    }
}
