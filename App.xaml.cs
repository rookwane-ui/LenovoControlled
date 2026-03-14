using LenovoController.Providers;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace LenovoController
{
    public partial class App : System.Windows.Application
    {
        public Settings Settings { get; private set; }
        public static App Instance { get; private set; }

        private static Mutex _mutex;
        private static EventWaitHandle _showEvent;

        // Static so the background listener thread can always reach it
        private static MainWindow _mainWindow;

        private NotifyIcon _notifyIcon;

        // ── Settings ──────────────────────────────────────────────────────────────
        public void LoadSettings()
        {
            try
            {
                if (File.Exists("settings.ini"))
                {
                    var json = File.ReadAllText("settings.ini");
                    Settings = JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
                }
                else
                {
                    Settings = new Settings();
                }
            }
            catch
            {
                Settings = new Settings();
            }
        }

        public void SaveSettings()
        {
            if (Settings != null)
                File.WriteAllText("settings.ini",
                    JsonConvert.SerializeObject(Settings, Formatting.Indented));
        }

        // ── Autostart ─────────────────────────────────────────────────────────────
        public bool CheckAutoStart()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
            {
                if (key == null) return false;
                var value = key.GetValue("LenovoController") as string;
                if (value == null) return false;
                var stored  = value.Trim('"');
                var current = Process.GetCurrentProcess().MainModule.FileName;
                return string.Equals(stored, current, StringComparison.OrdinalIgnoreCase);
            }
        }

        public void SetRunOnWindowsStartUp(bool autoStart)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key == null) return;
                if (autoStart)
                    key.SetValue("LenovoController",
                        $"\"{Process.GetCurrentProcess().MainModule.FileName}\"");
                else
                    key.DeleteValue("LenovoController", throwOnMissingValue: false);
            }
        }

        // ── Startup ───────────────────────────────────────────────────────────────
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Named event — second instance signals first instance through this
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset,
                "LenovoController_ShowWindow", out bool createdNew);

            _mutex = new Mutex(true, "LenovoController_SingleInstance", out bool isNew);

            if (!isNew)
            {
                // Already running — signal the first instance to show its window
                _showEvent.Set();
                Shutdown(0);
                return;
            }

            // First instance — start background thread listening for show signals
            var listenerThread = new Thread(() =>
            {
                while (true)
                {
                    _showEvent.WaitOne();
                    Dispatcher.Invoke(BringToFront);
                }
            })
            {
                IsBackground = true,
                Name = "ShowWindowListener"
            };
            listenerThread.Start();

            MigrateStartupRegistryIfNeeded();

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                var text = ex.ExceptionObject.ToString();
                Trace.TraceError(text);
                System.Windows.MessageBox.Show(text, "Lenovo Controller",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Instance = this;
            LoadSettings();

            string exitText = Settings.Culture switch
            {
                "RU" => "Выход",
                "UA" => "Вихід",
                _    => "Exit"
            };

            _notifyIcon = new NotifyIcon
            {
                Icon    = System.Drawing.Icon.FromHandle(LenovoController.Properties.Resources.LC.Handle),
                Visible = true,
                Text    = "Lenovo Controller",
                ContextMenu = new ContextMenu(new MenuItem[]
                {
                    new MenuItem("Lenovo Controller", OnAppRun),
                    new MenuItem(exitText, OnExitClick)
                })
            };

            // Single left click opens window directly
            _notifyIcon.Click += (s, ev) =>
            {
                if (ev is MouseEventArgs me && me.Button == MouseButtons.Left)
                    BringToFront();
            };

            CreateMainDialog();
        }

        // ── Window management ─────────────────────────────────────────────────────
        private void CreateMainDialog()
        {
            if (DriverProvider.ErrorShown) return;

            if (_mainWindow == null)
                _mainWindow = new MainWindow(this);

            if (!Settings.ShowOnStartup)
                _mainWindow.Hide();
            else
                _mainWindow.Show();
        }

        private void BringToFront()
        {
            if (DriverProvider.ErrorShown) return;

            if (_mainWindow == null)
                _mainWindow = new MainWindow(this);

            if (_mainWindow.Visibility != Visibility.Visible)
                _mainWindow.Show();

            _mainWindow.Activate();
            _mainWindow.Focus();
        }

        private void OnAppRun(object sender, EventArgs e) => BringToFront();

        private void OnExitClick(object sender, EventArgs e) => Shutdown(0);

        // ── Exit ──────────────────────────────────────────────────────────────────
        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            _showEvent?.Dispose();
            base.OnExit(e);
        }

        private void Application_DispatcherUnhandledException(object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            var text = e.Exception.ToString();
            Trace.TraceError(text);
            System.Windows.MessageBox.Show(text, "Lenovo Controller",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        // ── Registry migration ────────────────────────────────────────────────────
        private void MigrateStartupRegistryIfNeeded()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    var value = key.GetValue("LenovoController") as string;
                    if (value == null || value.StartsWith("\"")) return;
                    key.SetValue("LenovoController", $"\"{value}\"");
                    Trace.TraceInformation("Migrated startup registry entry to quoted path.");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Could not migrate startup registry: {ex.Message}");
            }
        }
    }
}
