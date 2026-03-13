using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LenovoController
{
    /// <summary>
    /// Applies Windows 11 Mica / Acrylic backdrop to a WPF window
    /// using only the DWM and DWMAPI Win32 APIs — no NuGet packages.
    /// </summary>
    public static class FluentWindow
    {
        // ── DWM enums ────────────────────────────────────────────────────────────
        private enum DWMWINDOWATTRIBUTE
        {
            DWMWA_USE_IMMERSIVE_DARK_MODE  = 20,
            DWMWA_WINDOW_CORNER_PREFERENCE = 33,
            DWMWA_BORDER_COLOR             = 34,
            DWMWA_CAPTION_COLOR            = 35,
            DWMWA_SYSTEMBACKDROP_TYPE      = 38,   // Windows 11 22H2+
        }

        private enum DWM_SYSTEMBACKDROP_TYPE
        {
            DWMSBT_AUTO        = 0,
            DWMSBT_NONE        = 1,
            DWMSBT_MAINWINDOW  = 2,   // Mica
            DWMSBT_TRANSIENT   = 3,   // Acrylic
            DWMSBT_TABBEDWINDOW = 4,  // Mica Alt
        }

        private enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT    = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND      = 2,
            DWMWCP_ROUNDSMALL = 3,
        }

        // ── P/Invoke ─────────────────────────────────────────────────────────────
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(
            IntPtr hwnd, ref MARGINS margins);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight;
        }

        // ── Windows version check ─────────────────────────────────────────────
        private static readonly Version Win11_22H2 = new Version(10, 0, 22621);
        private static readonly Version Win11_RTM  = new Version(10, 0, 22000);

        private static bool IsWin11_22H2OrHigher =>
            Environment.OSVersion.Version >= Win11_22H2;

        private static bool IsWin11OrHigher =>
            Environment.OSVersion.Version >= Win11_RTM;

        // ── Public API ────────────────────────────────────────────────────────────
        /// <summary>
        /// Call this in the window's Loaded event.
        /// Applies Mica (active) / Acrylic (inactive), native rounded corners,
        /// and dark/light title bar to match the app theme.
        /// </summary>
        public static void Apply(Window window, bool darkMode)
        {
            if (!IsWin11OrHigher) return;

            var hwnd = new WindowInteropHelper(window).EnsureHandle();

            // Make WPF background transparent so DWM backdrop shows through
            window.Background = Brushes.Transparent;
            var source = HwndSource.FromHwnd(hwnd);
            if (source != null)
                source.CompositionTarget.BackgroundColor = Colors.Transparent;

            // Extend DWM frame across entire client area
            var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1,
                                        cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);

            // Dark / light title bar
            SetDarkMode(hwnd, darkMode);

            // Rounded corners (Win11 default, but explicit is better)
            SetCornerPreference(hwnd, DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND);

            // Apply Mica on 22H2+, fall back to Acrylic on 21H2
            if (IsWin11_22H2OrHigher)
                SetBackdrop(hwnd, DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW);
            else
                SetBackdrop(hwnd, DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENT);

            // Switch to Acrylic on deactivation (like native Win11 apps)
            window.Activated   += (_, __) => OnActivated(hwnd);
            window.Deactivated += (_, __) => OnDeactivated(hwnd);
        }

        /// <summary>Call when the theme changes to re-apply dark mode to title bar.</summary>
        public static void UpdateTheme(Window window, bool darkMode)
        {
            if (!IsWin11OrHigher) return;
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            SetDarkMode(hwnd, darkMode);
        }

        // ── Private helpers ───────────────────────────────────────────────────────
        private static void OnActivated(IntPtr hwnd)
        {
            if (IsWin11_22H2OrHigher)
                SetBackdrop(hwnd, DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW);  // Mica
        }

        private static void OnDeactivated(IntPtr hwnd)
        {
            if (IsWin11_22H2OrHigher)
                SetBackdrop(hwnd, DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENT);   // Acrylic
        }

        private static void SetBackdrop(IntPtr hwnd, DWM_SYSTEMBACKDROP_TYPE type)
        {
            int value = (int)type;
            DwmSetWindowAttribute(hwnd,
                (int)DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE,
                ref value, sizeof(int));
        }

        private static void SetDarkMode(IntPtr hwnd, bool dark)
        {
            int value = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd,
                (int)DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref value, sizeof(int));
        }

        private static void SetCornerPreference(IntPtr hwnd, DWM_WINDOW_CORNER_PREFERENCE pref)
        {
            int value = (int)pref;
            DwmSetWindowAttribute(hwnd,
                (int)DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref value, sizeof(int));
        }
    }
}
