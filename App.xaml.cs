using LenovoController.Providers;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private NotifyIcon notifyIcon;
        private MainWindow mainWindow;

        public void LoadSettings()
        {
            try
            {
                if (File.Exists("settings.ini"))
                {
                    var json = File.ReadAllText("settings.ini");
                    Settings newSettings = JsonConvert.DeserializeObject<Settings>(json);
                    Settings = newSettings ?? new Settings();
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
            {
                var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                File.WriteAllText("settings.ini", json);
            }
        }

        public bool CheckAutoStart()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
            {
                if (key == null) return false;
                var value = key.GetValue("LenovoController") as string;
                if (value == null) return false;
                // Normalize: strip quotes for comparison
                var stored = value.Trim('"');
                var current = Assembly.GetExecutingAssembly().Location;
                return string.Equals(stored, current, StringComparison.OrdinalIgnoreCase);
            }
        }

        public void SetRunOnWindowsStartUp(bool autoStart)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key == null) return;
                if (autoStart)
                {
                    // Windows 11 requires quoted paths in startup registry
                    var path = Assembly.GetExecutingAssembly().Location;
                    key.SetValue("LenovoController", $"\"{path}\"");
                }
                else
                {
                    key.DeleteValue("LenovoController", throwOnMissingValue: false);
                }
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _mutex = new Mutex(true, "LenovoController_SingleInstance", out bool isNew);
            if (!isNew)
            {
                System.Windows.MessageBox.Show(
                    "Another copy of the application is already running. Close it and try again.",
                    "Lenovo Controller", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                var errorText = ex.ExceptionObject.ToString();
                Trace.TraceError(errorText);
                System.Windows.MessageBox.Show(errorText, "Lenovo Controller",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var errorText = e.Exception.ToString();
            Trace.TraceError(errorText);
            System.Windows.MessageBox.Show(errorText, "Lenovo Controller",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
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

            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = System.Drawing.Icon.FromHandle(
                LenovoController.Properties.Resources.LC.Handle);
            notifyIcon.Visible = true;
            notifyIcon.Text = "Lenovo Controller";

            // ── Single LEFT click opens the window directly ──────────────────
            notifyIcon.Click += (s, ev) =>
            {
                if (ev is MouseEventArgs me && me.Button == MouseButtons.Left)
                    OnAppRun(s, ev);
            };

            // ── Right-click context menu ─────────────────────────────────────
            notifyIcon.ContextMenu = new ContextMenu(new MenuItem[]
            {
                new MenuItem("Lenovo Controller", OnAppRun),
                new MenuItem(exitText, OnExitClick)
            });

            CreateMainDialog();
        }

        private void CreateMainDialog()
        {
            if (DriverProvider.ErrorShown) return;

            if (mainWindow == null)
                mainWindow = new MainWindow(this);

            if (!Settings.ShowOnStartup)
                mainWindow.Hide();
            else
                mainWindow.Show();
        }

        private void OnAppRun(object sender, EventArgs e)
        {
            if (DriverProvider.ErrorShown) return;

            if (mainWindow == null)
                mainWindow = new MainWindow(this);

            if (mainWindow.Visibility != Visibility.Visible)
            {
                mainWindow.Show();
                mainWindow.Activate();
            }
            else
            {
                mainWindow.Activate();
                mainWindow.Focus();
            }
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            Shutdown(0);
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            notifyIcon = null;
        }
    }
}
