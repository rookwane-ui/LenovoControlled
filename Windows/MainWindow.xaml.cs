using LenovoController.Features;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace LenovoController
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow, INotifyPropertyChanged
    {
        // ── Features ─────────────────────────────────────────────
        private readonly AlwaysOnUsbFeature  _alwaysOnUsbFeature  = new AlwaysOnUsbFeature();
        private readonly BatteryFeature      _batteryFeature      = new BatteryFeature();
        private readonly PowerModeFeature    _powerModeFeature    = new PowerModeFeature();
        private readonly FnLockFeature       _fnLockFeature       = new FnLockFeature();
        private readonly TouchpadLockFeature _touchpadLockFeature = new TouchpadLockFeature();
        private readonly MicrophoneFeature   _microphoneFeature   = new MicrophoneFeature();

        private RadioButton[] _batteryButtons;
        private RadioButton[] _powerModeButtons;
        private RadioButton[] _alwaysOnUsbButtons;

        private readonly App _app;
        private bool _refreshing;

        // ── INotifyPropertyChanged ───────────────────────────────
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string prop = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        // ── Constructor ──────────────────────────────────────────
        public MainWindow(App app)
        {
            InitializeComponent();

            _app = app;

            ChangeLanguage();

            _batteryButtons =
                new[] { radioConservation, radioNormalCharge, radioRapidCharge };

            _powerModeButtons =
                new[] { radioQuiet, radioBalance, radioPerformance };

            _alwaysOnUsbButtons =
                new[]
                {
                    radioAlwaysOnUsbOff,
                    radioAlwaysOnUsbOnWhenSleeping,
                    radioAlwaysOnUsbOnAlways
                };

            Loaded += async (_, __) => await RefreshAsync();
        }

        // ── Async Refresh ────────────────────────────────────────
        private async Task RefreshAsync()
        {
            btnRefresh.IsEnabled = false;
            _refreshing = true;

            try
            {
                BatteryState battery = default;
                AlwaysOnUsbState usb = default;
                PowerModeState powerMode = default;

                bool touchpad = false;
                bool fnLock = false;
                bool microphone = false;

                bool batteryOk = false;
                bool usbOk = false;
                bool powerOk = false;
                bool touchpadOk = false;
                bool fnLockOk = false;
                bool microphoneOk = false;

                await Task.Run(() =>
                {
                    Try(
                        () =>
                        {
                            powerMode = _powerModeFeature.GetAcState();
                            powerOk = true;
                        },
                        () => DisableControls(_powerModeButtons)
                    );

                    Try(
                        () =>
                        {
                            battery = _batteryFeature.GetState();
                            batteryOk = true;
                        },
                        () => DisableControls(_batteryButtons)
                    );

                    Try(
                        () =>
                        {
                            usb = _alwaysOnUsbFeature.GetState();
                            usbOk = true;
                        },
                        () => DisableControls(_alwaysOnUsbButtons)
                    );

                    Try(
                        () =>
                        {
                            touchpad =
                                _touchpadLockFeature.GetState()
                                == TouchpadLockState.Off;
                            touchpadOk = true;
                        },
                        () =>
                            Dispatcher.Invoke(
                                () => chkTouchpadLock.IsEnabled = false
                            )
                    );

                    Try(
                        () =>
                        {
                            fnLock =
                                _fnLockFeature.GetState()
                                == FnLockState.On;
                            fnLockOk = true;
                        },
                        () =>
                            Dispatcher.Invoke(
                                () => chkFnLock.IsEnabled = false
                            )
                    );
                });

                // ── Microphone check — on UI thread, with debug error popup ──
                Try(
                    () =>
                    {
                        if (!_microphoneFeature.IsSupported())
                            throw new Exception("IsSupported() returned false — no active capture devices found");

                        microphone = _microphoneFeature.GetState();
                        microphoneOk = true;
                    },
                    () =>
                    {
                        // DEBUG: show exact exception so we can fix the real cause
                        try { _microphoneFeature.GetState(); }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.ToString(), "Mic GetState() failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        chkMicrophone.IsEnabled = false;
                    }
                );

                if (powerOk)
                    _powerModeButtons[(int)powerMode].IsChecked = true;

                if (batteryOk)
                    _batteryButtons[(int)battery].IsChecked = true;

                if (usbOk)
                    _alwaysOnUsbButtons[(int)usb].IsChecked = true;

                if (touchpadOk)
                    chkTouchpadLock.IsChecked = touchpad;

                if (fnLockOk)
                    chkFnLock.IsChecked = fnLock;

                if (microphoneOk)
                    chkMicrophone.IsChecked = microphone;
            }
            finally
            {
                _refreshing = false;
                btnRefresh.IsEnabled = true;
            }
        }

        private static void Try(Action check, Action disable)
        {
            try
            {
                check();
            }
            catch (Exception e)
            {
                Trace.TraceWarning("Feature unavailable: " + e.Message);
                disable();
            }
        }

        private void DisableControls(Control[] controls)
        {
            foreach (var c in controls)
                Dispatcher.Invoke(() => c.IsEnabled = false);
        }

        // ── Button handlers ──────────────────────────────────────
        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            var handle = new WindowInteropHelper(this).EnsureHandle();
            var dlg = new AboutWindow(handle);
            dlg.ShowDialog();
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e) =>
            await RefreshAsync();

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var handle = new WindowInteropHelper(this).EnsureHandle();

            var dlg = new SettingsWindow(handle, _app);

            dlg.ShowDialog();

            if (dlg.DialogResult == true)
            {
                _app.ApplyTheme(_app.Settings.DarkMode);
                ChangeLanguage();
            }
        }

        private void btnExit_Click(object sender, RoutedEventArgs e) =>
            _app.Shutdown(0);

        // ── Feature handlers ─────────────────────────────────────
        private void radioPowerMode_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing)
                return;

            var newState =
                (PowerModeState)Array.IndexOf(_powerModeButtons, sender);

            SafeSet(() => _powerModeFeature.SetAcState(newState));
        }

        private void radioBattery_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing)
                return;

            var newState =
                (BatteryState)Array.IndexOf(_batteryButtons, sender);

            if (_batteryFeature.GetState() != newState)
                SafeSet(() => _batteryFeature.SetState(newState));
        }

        private void radioAlwaysOnUsb_Checked(
            object sender,
            RoutedEventArgs e
        )
        {
            if (_refreshing)
                return;

            var newState =
                (AlwaysOnUsbState)
                    Array.IndexOf(_alwaysOnUsbButtons, sender);

            if (_alwaysOnUsbFeature.GetState() != newState)
                SafeSet(() => _alwaysOnUsbFeature.SetState(newState));
        }

        private void chkTouchpadLock_Checked(
            object sender,
            RoutedEventArgs e
        )
        {
            if (_refreshing)
                return;

            var newState =
                chkTouchpadLock.IsChecked == true
                    ? TouchpadLockState.Off
                    : TouchpadLockState.On;

            if (_touchpadLockFeature.GetState() != newState)
                SafeSet(() => _touchpadLockFeature.SetState(newState));
        }

        private void chkFnLock_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing)
                return;

            var newState =
                chkFnLock.IsChecked == true
                    ? FnLockState.On
                    : FnLockState.Off;

            if (_fnLockFeature.GetState() != newState)
                SafeSet(() => _fnLockFeature.SetState(newState));
        }

        private void chkMicrophone_Checked(
            object sender,
            RoutedEventArgs e
        )
        {
            if (_refreshing)
                return;

            bool newState = chkMicrophone.IsChecked == true;

            if (_microphoneFeature.GetState() != newState)
                SafeSet(() => _microphoneFeature.SetState(newState));
        }

        private static void SafeSet(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Trace.TraceError(
                    "Failed to set feature: " + e.Message
                );
            }
        }

        private void Window_Closing(
            object sender,
            CancelEventArgs e
        )
        {
            Hide();
            e.Cancel = true;
        }

        // ── Localisation (unchanged) ─────────────────────────────
        private void ChangeLanguage()
        {
            switch (_app.Settings.Culture)
            {
                case "RU":
                    batteryGroup.Text = "Зарядка батареи";
                    miscGroup.Text = "Дополнительные опции";
                    btnAbout.Content = "О программе";
                    btnExit.Content = "Выход";
                    break;

                case "UA":
                    batteryGroup.Text = "Зарядка батареї";
                    miscGroup.Text = "Додаткові опції";
                    btnAbout.Content = "Про програму";
                    btnExit.Content = "Вихід";
                    break;

                default:
                    batteryGroup.Text = "Battery charge";
                    miscGroup.Text = "Additional options";
                    btnAbout.Content = "About";
                    btnExit.Content = "Exit";
                    break;
            }
        }
    }
}
