using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;

namespace LenovoController
{
    public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow, INotifyPropertyChanged
    {
        private readonly App _app;
        private bool _applyPressed;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string p = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public SettingsWindow(IntPtr ownerHandle, App app)
        {
            InitializeComponent();
            _app = app;

            new WindowInteropHelper(this).Owner = ownerHandle;

            darkTheme.IsChecked    = app.Settings.DarkMode;
            autoRun.IsChecked      = app.CheckAutoStart();
            showOnStartup.IsChecked = app.Settings.ShowOnStartup;

            ChangeLanguage();
        }

        private void ChangeLanguage()
        {
            switch (_app.Settings.Culture)
            {
                case "RU":
                    langList.ItemsSource   = new[] { "Русский", "Украинский", "Английский" };
                    langList.SelectedIndex = 0;
                    Title                  = "Настройки";
                    btnApply.Content       = "Применить";
                    btnCancel.Content      = "Отмена";
                    break;
                case "UA":
                    langList.ItemsSource   = new[] { "Російська", "Українська", "Англійська" };
                    langList.SelectedIndex = 1;
                    Title                  = "Налаштування";
                    btnApply.Content       = "Примінити";
                    btnCancel.Content      = "Відміна";
                    break;
                default:
                    langList.ItemsSource   = new[] { "Russian", "Ukrainian", "English" };
                    langList.SelectedIndex = 2;
                    Title                  = "Settings";
                    btnApply.Content       = "Apply";
                    btnCancel.Content      = "Cancel";
                    break;
            }
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            string lang = langList.SelectedIndex switch
            {
                0 => "RU",
                1 => "UA",
                _ => "EN"
            };

            _app.Settings.Culture      = lang;
            _app.Settings.DarkMode     = darkTheme.IsChecked == true;
            _app.Settings.ShowOnStartup = showOnStartup.IsChecked == true;
            _app.SaveSettings();
            _app.SetRunOnWindowsStartUp(autoRun.IsChecked == true);

            _applyPressed = true;
            DialogResult  = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try { DialogResult = _applyPressed; }
            catch (InvalidOperationException) { }
        }
    }
}
