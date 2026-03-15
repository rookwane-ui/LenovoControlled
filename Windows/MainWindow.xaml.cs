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
        // ── Features ──────────────────────────────────────────────────────────────
        private readonly AlwaysOnUsbFeature  _alwaysOnUsbFeature  = new AlwaysOnUsbFeature();
        private readonly BatteryFeature      _batteryFeature      = new BatteryFeature();
        private readonly PowerModeFeature    _powerModeFeature    = new PowerModeFeature();
        private readonly FnLockFeature       _fnLockFeature       = new FnLockFeature();
        private readonly TouchpadLockFeature _touchpadLockFeature = new TouchpadLockFeature();

        private RadioButton[] _batteryButtons;
        private RadioButton[] _powerModeAcButtons;
        private RadioButton[] _powerModeDcButtons;
        private RadioButton[] _alwaysOnUsbButtons;

        private readonly App _app;
        private bool _refreshing;

        // ── INotifyPropertyChanged ────────────────────────────────────────────────
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        // ── Constructor ───────────────────────────────────────────────────────────
        public MainWindow(App app)
        {
            InitializeComponent();
            _app = app;
            ChangeLanguage();

            _batteryButtons     = new[] { radioConservation, radioNormalCharge, radioRapidCharge };
            _powerModeAcButtons = new[] { radioAcQuiet, radioAcBalance, radioAcPerformance };
            _powerModeDcButtons = new[] { radioDcQuiet, radioDcBalance, radioDcPerformance };
            _alwaysOnUsbButtons = new[] { radioAlwaysOnUsbOff, radioAlwaysOnUsbOnWhenSleeping, radioAlwaysOnUsbOnAlways };

            Loaded += async (_, __) => await RefreshAsync();
        }

        // ── Async Refresh ─────────────────────────────────────────────────────────
        private async Task RefreshAsync()
        {
            btnRefresh.IsEnabled = false;
            _refreshing = true;

            try
            {
                BatteryState     battery    = default;
                AlwaysOnUsbState usb        = default;
                PowerModeState   powerAc    = default;
                PowerModeState   powerDc    = default;
                bool touchpad = false, fnLock = false;
                bool batteryOk = false, usbOk = false,
                     powerAcOk = false, powerDcOk = false,
                     touchpadOk = false, fnLockOk = false;

                await Task.Run(() =>
                {
                    Try(() => { powerAc  = _powerModeFeature.GetAcState();                            powerAcOk  = true; }, () => DisableControls(_powerModeAcButtons));
                    Try(() => { powerDc  = _powerModeFeature.GetDcState();                            powerDcOk  = true; }, () => DisableControls(_powerModeDcButtons));
                    Try(() => { battery  = _batteryFeature.GetState();                                batteryOk  = true; }, () => DisableControls(_batteryButtons));
                    Try(() => { usb      = _alwaysOnUsbFeature.GetState();                            usbOk      = true; }, () => DisableControls(_alwaysOnUsbButtons));
                    Try(() => { touchpad = _touchpadLockFeature.GetState() == TouchpadLockState.Off;  touchpadOk = true; }, () => Dispatcher.Invoke(() => chkTouchpadLock.IsEnabled = false));
                    Try(() => { fnLock   = _fnLockFeature.GetState()       == FnLockState.On;         fnLockOk   = true; }, () => Dispatcher.Invoke(() => chkFnLock.IsEnabled        = false));
                });

                if (powerAcOk)  _powerModeAcButtons[(int)powerAc].IsChecked = true;
                if (powerDcOk)  _powerModeDcButtons[(int)powerDc].IsChecked = true;
                if (batteryOk)  _batteryButtons[(int)battery].IsChecked      = true;
                if (usbOk)      _alwaysOnUsbButtons[(int)usb].IsChecked      = true;
                if (touchpadOk) chkTouchpadLock.IsChecked = touchpad;
                if (fnLockOk)   chkFnLock.IsChecked        = fnLock;
            }
            finally
            {
                _refreshing = false;
                btnRefresh.IsEnabled = true;
            }
        }

        private static void Try(Action check, Action disable)
        {
            try   { check(); }
            catch (Exception e) { Trace.TraceWarning("Feature unavailable: " + e.Message); disable(); }
        }

        private void DisableControls(Control[] controls)
        {
            foreach (var c in controls)
                Dispatcher.Invoke(() => c.IsEnabled = false);
        }

        // ── Button handlers ───────────────────────────────────────────────────────
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

        // ── Feature handlers ─────────────────────────────────────────────────────
        private void radioPowerModeAC_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            var newState = (PowerModeState)Array.IndexOf(_powerModeAcButtons, sender);
            SafeSet(() => _powerModeFeature.SetAcState(newState));
        }

        private void radioPowerModeDC_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            var newState = (PowerModeState)Array.IndexOf(_powerModeDcButtons, sender);
            SafeSet(() => _powerModeFeature.SetDcState(newState));
        }

        private void radioBattery_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            var newState = (BatteryState)Array.IndexOf(_batteryButtons, sender);
            if (_batteryFeature.GetState() != newState)
                SafeSet(() => _batteryFeature.SetState(newState));
        }

        private void radioAlwaysOnUsb_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            var newState = (AlwaysOnUsbState)Array.IndexOf(_alwaysOnUsbButtons, sender);
            if (_alwaysOnUsbFeature.GetState() != newState)
                SafeSet(() => _alwaysOnUsbFeature.SetState(newState));
        }

        private void chkTouchpadLock_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            var newState = chkTouchpadLock.IsChecked == true
                ? TouchpadLockState.Off : TouchpadLockState.On;
            if (_touchpadLockFeature.GetState() != newState)
                SafeSet(() => _touchpadLockFeature.SetState(newState));
        }

        private void chkFnLock_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            var newState = chkFnLock.IsChecked == true ? FnLockState.On : FnLockState.Off;
            if (_fnLockFeature.GetState() != newState)
                SafeSet(() => _fnLockFeature.SetState(newState));
        }

        private static void SafeSet(Action action)
        {
            try   { action(); }
            catch (Exception e) { Trace.TraceError("Failed to set feature: " + e.Message); }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }

        // ── Localisation ──────────────────────────────────────────────────────────
        private void ChangeLanguage()
        {
            switch (_app.Settings.Culture)
            {
                case "RU":
                    batteryGroup.Text              = "Зарядка батареи";
                    radioConservation.Content      = "Сбережение";
                    radioNormalCharge.Content      = "Нормальная";
                    radioRapidCharge.Content       = "Быстрая";
                    powerModeGroup.Text            = "Режим питания";
                    powerAcTitle.Text              = "От сети";
                    powerAcSubtitle.Text           = "Режим при зарядке";
                    powerDcTitle.Text              = "От батареи";
                    powerDcSubtitle.Text           = "Режим при работе от батареи";
                    radioAcQuiet.Content           = "Экономия";
                    radioAcBalance.Content         = "Баланс";
                    radioAcPerformance.Content     = "Производительность";
                    radioDcQuiet.Content           = "Экономия";
                    radioDcBalance.Content         = "Баланс";
                    radioDcPerformance.Content     = "Производительность";
                    alwaysGroup.Text               = "Always on USB";
                    radioAlwaysOnUsbOff.Content            = "Выкл.";
                    radioAlwaysOnUsbOnWhenSleeping.Content = "Во сне";
                    radioAlwaysOnUsbOnAlways.Content       = "Всегда";
                    miscGroup.Text                 = "Дополнительные опции";
                    fnLockTitle.Text               = "Fn Lock";
                    fnLockSubtitle.Text            = "Эмулирует удержание клавиши Fn";
                    touchpadTitle.Text             = "Тачпад";
                    touchpadSubtitle.Text          = "Включить или отключить тачпад";
                    btnRefresh.Content             = "Обновить";
                    btnExit.Content                = "Выход";
                    break;

                case "UA":
                    batteryGroup.Text              = "Зарядка батареї";
                    radioConservation.Content      = "Збереження";
                    radioNormalCharge.Content      = "Нормальна";
                    radioRapidCharge.Content       = "Швидка";
                    powerModeGroup.Text            = "Режим живлення";
                    powerAcTitle.Text              = "Від мережі";
                    powerAcSubtitle.Text           = "Режим під час зарядки";
                    powerDcTitle.Text              = "Від батареї";
                    powerDcSubtitle.Text           = "Режим під час роботи від батареї";
                    radioAcQuiet.Content           = "Економія";
                    radioAcBalance.Content         = "Баланс";
                    radioAcPerformance.Content     = "Продуктивність";
                    radioDcQuiet.Content           = "Економія";
                    radioDcBalance.Content         = "Баланс";
                    radioDcPerformance.Content     = "Продуктивність";
                    alwaysGroup.Text               = "Always on USB";
                    radioAlwaysOnUsbOff.Content            = "Вимк.";
                    radioAlwaysOnUsbOnWhenSleeping.Content = "Уві сні";
                    radioAlwaysOnUsbOnAlways.Content       = "Завжди";
                    miscGroup.Text                 = "Додаткові опції";
                    fnLockTitle.Text               = "Fn Lock";
                    fnLockSubtitle.Text            = "Емулює утримання клавіші Fn";
                    touchpadTitle.Text             = "Тачпад";
                    touchpadSubtitle.Text          = "Увімкнути або вимкнути тачпад";
                    btnRefresh.Content             = "Оновити";
                    btnExit.Content                = "Вихід";
                    break;

                default:
                    batteryGroup.Text              = "Battery charge";
                    radioConservation.Content      = "Conservation";
                    radioNormalCharge.Content      = "Normal";
                    radioRapidCharge.Content       = "Rapid";
                    powerModeGroup.Text            = "Power mode";
                    powerAcTitle.Text              = "Plugged in";
                    powerAcSubtitle.Text           = "Power mode when charging";
                    powerDcTitle.Text              = "On battery";
                    powerDcSubtitle.Text           = "Power mode when on battery";
                    radioAcQuiet.Content           = "Efficiency";
                    radioAcBalance.Content         = "Balanced";
                    radioAcPerformance.Content     = "Performance";
                    radioDcQuiet.Content           = "Efficiency";
                    radioDcBalance.Content         = "Balanced";
                    radioDcPerformance.Content     = "Performance";
                    alwaysGroup.Text               = "Always on USB";
                    radioAlwaysOnUsbOff.Content            = "Off";
                    radioAlwaysOnUsbOnWhenSleeping.Content = "Sleeping";
                    radioAlwaysOnUsbOnAlways.Content       = "Always";
                    miscGroup.Text                 = "Additional options";
                    fnLockTitle.Text               = "Fn Lock";
                    fnLockSubtitle.Text            = "Emulates holding the Fn key permanently";
                    touchpadTitle.Text             = "Touchpad";
                    touchpadSubtitle.Text          = "Enable or disable the touchpad";
                    btnRefresh.Content             = "Refresh";
                    btnExit.Content                = "Exit";
                    break;
            }
        }
    }
}
