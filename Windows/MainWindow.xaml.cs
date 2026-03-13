using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using LenovoController.Features;

namespace LenovoController
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // ── Features ──────────────────────────────────────────────────────────────
        private readonly AlwaysOnUsbFeature  _alwaysOnUsbFeature  = new AlwaysOnUsbFeature();
        private readonly BatteryFeature      _batteryFeature      = new BatteryFeature();
        private readonly PowerModeFeature    _powerModeFeature    = new PowerModeFeature();
        private readonly FnLockFeature       _fnLockFeature       = new FnLockFeature();
        private readonly TouchpadLockFeature _touchpadLockFeature = new TouchpadLockFeature();

        private RadioButton[] _batteryButtons;
        private RadioButton[] _powerModeButtons;
        private RadioButton[] _alwaysOnUsbButtons;

        private readonly App _app;
        private bool _refreshing;

        // ── INotifyPropertyChanged ────────────────────────────────────────────────
        private bool _darkMode;
        public bool DarkMode
        {
            get => _darkMode;
            set
            {
                _darkMode = value;
                OnPropertyChanged();
                FluentWindow.UpdateTheme(this, value);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        // ── Constructor ───────────────────────────────────────────────────────────
        public MainWindow(App app)
        {
            InitializeComponent();
            DataContext = this;

            _app = app;
            _darkMode = _app.Settings.DarkMode;
            ChangeLanguage();

            _powerModeButtons   = new[] { radioQuiet, radioBalance, radioPerformance };
            _batteryButtons     = new[] { radioConservation, radioNormalCharge, radioRapidCharge };
            _alwaysOnUsbButtons = new[] { radioAlwaysOnUsbOff, radioAlwaysOnUsbOnWhenSleeping, radioAlwaysOnUsbOnAlways };

            Loaded += async (_, __) =>
            {
                FluentWindow.Apply(this, _darkMode);
                await RefreshAsync();
            };
        }

        // ── Async Refresh ─────────────────────────────────────────────────────────
        private async Task RefreshAsync()
        {
            btnRefresh.IsEnabled = false;
            _refreshing = true;

            try
            {
                PowerModeState   powerMode = default;
                BatteryState     battery   = default;
                AlwaysOnUsbState usb       = default;
                bool touchpad = false, fnLock = false;
                bool powerOk = false, batteryOk = false, usbOk = false,
                     touchpadOk = false, fnLockOk = false;

                await Task.Run(() =>
                {
                    Try(() => { powerMode = _powerModeFeature.GetState();                             powerOk    = true; }, () => DisableControls(_powerModeButtons));
                    Try(() => { battery   = _batteryFeature.GetState();                               batteryOk  = true; }, () => DisableControls(_batteryButtons));
                    Try(() => { usb       = _alwaysOnUsbFeature.GetState();                           usbOk      = true; }, () => DisableControls(_alwaysOnUsbButtons));
                    Try(() => { touchpad  = _touchpadLockFeature.GetState() == TouchpadLockState.Off; touchpadOk = true; }, () => Dispatcher.Invoke(() => chkTouchpadLock.IsEnabled = false));
                    Try(() => { fnLock    = _fnLockFeature.GetState()       == FnLockState.On;        fnLockOk   = true; }, () => Dispatcher.Invoke(() => chkFnLock.IsEnabled        = false));
                });

                if (powerOk)   _powerModeButtons[(int)powerMode].IsChecked  = true;
                if (batteryOk) _batteryButtons[(int)battery].IsChecked       = true;
                if (usbOk)     _alwaysOnUsbButtons[(int)usb].IsChecked       = true;
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
                DarkMode = _app.Settings.DarkMode;
                ChangeLanguage();
            }
        }

        private void btnExit_Click(object sender, RoutedEventArgs e) =>
            _app.Shutdown(0);

        // ── Feature handlers ─────────────────────────────────────────────────────
        private void radioPowerMode_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            var newState = (PowerModeState)Array.IndexOf(_powerModeButtons, sender);
            if (_powerModeFeature.GetState() != newState)
                SafeSet(() => _powerModeFeature.SetAndSyncState(newState));
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
            var newState = chkTouchpadLock.IsChecked == true ? TouchpadLockState.Off : TouchpadLockState.On;
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
                    radioConservation.ToolTip      = "Батарея не заряжается выше 60% для сбережения ресурса";
                    radioNormalCharge.Content      = "Нормальная";
                    radioNormalCharge.ToolTip      = "Батарея заряжается до 100% как обычно";
                    radioRapidCharge.Content       = "Быстрая";
                    radioRapidCharge.ToolTip       = "Батарея заряжается быстрее. Может вызывать нагрев блока питания";
                    powerModeGroup.Text            = "Режим работы";
                    radioQuiet.Content             = "Тихий";
                    radioQuiet.ToolTip             = "Ограничивает мощность CPU и GPU, система охлаждения работает тихо";
                    radioBalance.Content           = "Авто";
                    radioBalance.ToolTip           = "Мощность CPU и GPU регулируются автоматически";
                    radioPerformance.Content       = "Производительность";
                    radioPerformance.ToolTip       = "Максимальная мощность CPU и GPU, вентиляторы работают громче";
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
                    btnRefresh.ToolTip             = "Синхронизировать настройки с ноутбуком";
                    btnSettings.ToolTip            = "Настройки";
                    btnExit.Content                = "Выход";
                    break;

                case "UA":
                    batteryGroup.Text              = "Зарядка батареї";
                    radioConservation.Content      = "Збереження";
                    radioConservation.ToolTip      = "Батарея не заряджається вище 60% для збереження ресурсу";
                    radioNormalCharge.Content      = "Нормальна";
                    radioNormalCharge.ToolTip      = "Батарея заряджається до 100% як зазвичай";
                    radioRapidCharge.Content       = "Швидка";
                    radioRapidCharge.ToolTip       = "Батарея заряджається швидше. Може викликати нагрів БЖ";
                    powerModeGroup.Text            = "Режим роботи";
                    radioQuiet.Content             = "Тихий";
                    radioQuiet.ToolTip             = "Обмежує потужність CPU і GPU, система охолодження тиха";
                    radioBalance.Content           = "Авто";
                    radioBalance.ToolTip           = "Потужність CPU і GPU регулюються автоматично";
                    radioPerformance.Content       = "Продуктивність";
                    radioPerformance.ToolTip       = "Максимальна потужність CPU і GPU";
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
                    btnRefresh.ToolTip             = "Синхронізувати налаштування з ноутбуком";
                    btnSettings.ToolTip            = "Налаштування";
                    btnExit.Content                = "Вихід";
                    break;

                default:
                    batteryGroup.Text              = "Battery charge";
                    radioConservation.Content      = "Conservation";
                    radioConservation.ToolTip      = "Battery won't charge above 60% to extend its lifespan";
                    radioNormalCharge.Content      = "Normal";
                    radioNormalCharge.ToolTip      = "Battery charges to 100% as usual";
                    radioRapidCharge.Content       = "Rapid";
                    radioRapidCharge.ToolTip       = "Battery charges faster. May cause the power adapter to run warm";
                    powerModeGroup.Text            = "Power mode";
                    radioQuiet.Content             = "Quiet";
                    radioQuiet.ToolTip             = "Limits CPU/GPU power; fans run quietly";
                    radioBalance.Content           = "Balance";
                    radioBalance.ToolTip           = "CPU/GPU power adjusts automatically for most tasks";
                    radioPerformance.Content       = "Performance";
                    radioPerformance.ToolTip       = "Maximum CPU/GPU power; fans run at full speed";
                    alwaysGroup.Text               = "Always on USB";
                    radioAlwaysOnUsbOff.Content            = "Off";
                    radioAlwaysOnUsbOff.ToolTip            = "USB ports have no power when the laptop is off";
                    radioAlwaysOnUsbOnWhenSleeping.Content = "Sleeping";
                    radioAlwaysOnUsbOnWhenSleeping.ToolTip = "USB ports are powered during sleep";
                    radioAlwaysOnUsbOnAlways.Content       = "Always";
                    radioAlwaysOnUsbOnAlways.ToolTip       = "USB ports are always powered";
                    miscGroup.Text                 = "Additional options";
                    fnLockTitle.Text               = "Fn Lock";
                    fnLockSubtitle.Text            = "Emulates holding the Fn key permanently";
                    touchpadTitle.Text             = "Touchpad";
                    touchpadSubtitle.Text          = "Enable or disable the touchpad";
                    btnRefresh.Content             = "Refresh";
                    btnRefresh.ToolTip             = "Sync settings with the laptop hardware";
                    btnSettings.ToolTip            = "Settings";
                    btnExit.Content                = "Exit";
                    break;
            }
        }
    }
}
