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
using Wpf.Ui.Appearance;

namespace LenovoController
{
    public partial class App : System.Windows.Application
    {
        public Settings Settings { get; private set; }
        public static App Instance { get; private set; }

        private static Mutex _mutex;
        private static EventWaitHandle _showEvent;
        private static MainWindow _mainWindow;
        private NotifyIcon _notifyIcon;

        public void LoadSettings()
        {
            try
            {
                if (File.Exists("settings.ini"))
                    Settings = JsonConvert.DeserializeObject<Settings>(
                        File.ReadAllText("settings.ini")) ?? new Settings();
                else
                    Settings = new Settings();
            }
            catch { Settings = new Settings(); }
        }

        public void SaveSettings()
        {
            if (Settings != null)
                File.WriteAllText("settings.ini",
                    JsonConvert.SerializeObject(Settings, Formatting.Indented));
        }

        public bool CheckAutoStart()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            if (key == null) return false;
            var value = key.GetValue("LenovoController") as string;
            if (value == null) return false;
            return string.Equals(value.Trim('"'),
                Process.GetCurrentProcess().MainModule.FileName,
                StringComparison.OrdinalIgnoreCase);
        }

        public void SetRunOnWindowsStartUp(bool autoStart)
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (autoStart)
                key.SetValue("LenovoController",
                    $"\"{Process.GetCurrentProcess().MainModule.FileName}\"");
            else
                key.DeleteValue("LenovoController", throwOnMissingValue: false);
        }

        public void ApplyTheme(bool dark)
        {
            ApplicationThemeManager.Apply(
                dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset,
                "LenovoController_ShowWindow", out _);
            _mutex = new Mutex(true, "LenovoController_SingleInstance", out bool isNew);

            if (!isNew)
            {
                _showEvent.Set();
                Shutdown(0);
                return;
            }

            var listener = new Thread(() =>
            {
                while (true) { _showEvent.WaitOne(); Dispatcher.Invoke(BringToFront); }
            }) { IsBackground = true, Name = "ShowWindowListener" };
            listener.Start();

            MigrateStartupRegistryIfNeeded();

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                Trace.TraceError(ex.ExceptionObject.ToString());
                System.Windows.MessageBox.Show(ex.ExceptionObject.ToString(),
                    "Lenovo Controller", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Instance = this;
            LoadSettings();
            ApplyTheme(Settings.DarkMode);

            string exitText = Settings.Culture switch
            {
                "RU" => "Выход",
                "UA" => "Вихід",
                _    => "Exit"
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Lenovo Controller", null, (s, ev) => BringToFront());
            menu.Items.Add(exitText, null, (s, ev) => Shutdown(0));

            _notifyIcon = new NotifyIcon
            {
                Icon             = System.Drawing.Icon.ExtractAssociatedIcon(
                                       Process.GetCurrentProcess().MainModule.FileName),
                Visible          = true,
                Text             = "Lenovo Controller",
                ContextMenuStrip = menu
            };
            _notifyIcon.Click += (s, ev) =>
            {
                if (ev is MouseEventArgs me && me.Button == MouseButtons.Left)
                    BringToFront();
            };

            CreateMainDialog();
        }

        private void CreateMainDialog()
        {
            if (DriverProvider.ErrorShown) return;
            if (_mainWindow == null) _mainWindow = new MainWindow(this);
            if (!Settings.ShowOnStartup) _mainWindow.Hide();
            else _mainWindow.Show();
        }

        public void BringToFront()
        {
            if (DriverProvider.ErrorShown) return;
            if (_mainWindow == null) _mainWindow = new MainWindow(this);
            if (_mainWindow.Visibility != Visibility.Visible) _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.Focus();
        }

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
            Trace.TraceError(e.Exception.ToString());
            System.Windows.MessageBox.Show(e.Exception.ToString(),
                "Lenovo Controller", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
        }

        private void MigrateStartupRegistryIfNeeded()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;
                var value = key.GetValue("LenovoController") as string;
                if (value == null || value.StartsWith("\"")) return;
                key.SetValue("LenovoController", $"\"{value}\"");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Registry migration failed: {ex.Message}");
            }
        }
    }
}
