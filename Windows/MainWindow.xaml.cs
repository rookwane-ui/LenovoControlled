using LenovoController.Features;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace LenovoController
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly AlwaysOnUsbFeature _alwaysOnUsbFeature = new();
        private readonly BatteryFeature _batteryFeature = new();
        private readonly PowerModeFeature _powerModeFeature = new();
        private readonly FnLockFeature _fnLockFeature = new();
        private readonly TouchpadLockFeature _touchpadLockFeature = new();

        private bool _refreshing;
        private readonly App _app;

        public MainWindow(App app)
        {
            InitializeComponent();
            _app = app;

            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Loaded += async (_, _) => await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            _refreshing = true;

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            chkFnLock.IsChecked = _fnLockFeature.GetState() == FnLockState.On;
                            chkTouchpadLock.IsChecked = _touchpadLockFeature.GetState() == TouchpadLockState.Off;
                        });
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceWarning(ex.Message);
                    }
                });
            }
            finally
            {
                _refreshing = false;
            }
        }

        private async void RadioGroup_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            if (sender is not RadioButton rb || rb.Tag is not string type) return;

            try
            {
                int value = int.Parse(rb.CommandParameter.ToString());

                await Task.Run(() =>
                {
                    switch (type)
                    {
                        case "PowerMode":
                            _powerModeFeature.SetAcState((PowerModeState)value);
                            break;
                        case "Battery":
                            _batteryFeature.SetState((BatteryState)value);
                            break;
                        case "Usb":
                            _alwaysOnUsbFeature.SetState((AlwaysOnUsbState)value);
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
            }
        }

        private void chkFnLock_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;

            SafeSet(() => _fnLockFeature.SetState(
                chkFnLock.IsChecked == true ? FnLockState.On : FnLockState.Off));
        }

        private void chkTouchpadLock_Checked(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;

            SafeSet(() => _touchpadLockFeature.SetState(
                chkTouchpadLock.IsChecked == true ? TouchpadLockState.Off : TouchpadLockState.On));
        }

        private static void SafeSet(Action action)
        {
            try { action(); }
            catch (Exception ex) { Trace.TraceError(ex.Message); }
        }

        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AboutWindow(new WindowInteropHelper(this).EnsureHandle());
            dlg.ShowDialog();
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(new WindowInteropHelper(this).EnsureHandle(), _app);
            dlg.ShowDialog();
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            _app.Shutdown(0);
        }
    }
}
