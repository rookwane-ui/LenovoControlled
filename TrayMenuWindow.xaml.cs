using LenovoController.Features;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace LenovoController
{
    public partial class TrayMenuWindow : Window, INotifyPropertyChanged
    {
        private readonly App _app;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string p = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private bool _darkMode;
        public bool DarkMode
        {
            get => _darkMode;
            set { _darkMode = value; OnPropertyChanged(); }
        }

        public TrayMenuWindow(App app)
        {
            InitializeComponent();
            DataContext = this;
            _app = app;
            DarkMode = app.Settings.DarkMode;
            UpdateLabels();
            UpdateStatus();
        }

        private void UpdateLabels()
        {
            switch (_app.Settings.Culture)
            {
                case "RU":
                    openLabel.Text     = "Открыть";
                    settingsLabel.Text = "Настройки";
                    exitLabel.Text     = "Выход";
                    break;
                case "UA":
                    openLabel.Text     = "Відкрити";
                    settingsLabel.Text = "Налаштування";
                    exitLabel.Text     = "Вихід";
                    break;
                default:
                    openLabel.Text     = "Open";
                    settingsLabel.Text = "Settings";
                    exitLabel.Text     = "Exit";
                    break;
            }
        }

        private void UpdateStatus()
        {
            try
            {
                var state = new PowerModeFeature().GetAcState();
                statusText.Text = state switch
                {
                    PowerModeState.Quiet       => "Best power efficiency",
                    PowerModeState.Performance => "Best performance",
                    _                          => "Balanced"
                };
            }
            catch
            {
                statusText.Text = "Running";
            }
        }

        public void ShowAtCursor()
        {
            // Force layout so we know ActualHeight before positioning
            Measure(new Size(220, double.PositiveInfinity));
            Arrange(new Rect(DesiredSize));

            var cursor = System.Windows.Forms.Cursor.Position;

            // DPI scale factor
            double dpi = 1.0;
            using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                dpi = g.DpiX / 96.0;

            double w = 220;
            double h = DesiredSize.Height > 0 ? DesiredSize.Height : 200;

            double left = cursor.X / dpi - w + 4;
            double top  = cursor.Y / dpi - h - 4;

            // Keep on screen
            var area = SystemParameters.WorkArea;
            if (left < area.Left)   left = area.Left + 4;
            if (left + w > area.Right) left = area.Right - w - 4;
            if (top  < area.Top)    top  = cursor.Y / dpi + 4;

            Left = left;
            Top  = top;

            Show();
            Activate();
        }

        private bool _closing;

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (!_closing) { _closing = true; Close(); }
        }

        private void BtnOpen_Click(object sender, MouseButtonEventArgs e)
        {
            _closing = true;
            Close();
            _app.BringToFront();
        }

        private void BtnSettings_Click(object sender, MouseButtonEventArgs e)
        {
            _closing = true;
            Close();
            _app.OpenSettings();
        }

        private void BtnExit_Click(object sender, MouseButtonEventArgs e)
        {
            _closing = true;
            Deactivated -= Window_Deactivated;
            _app.Shutdown(0);
        }
    }
}
