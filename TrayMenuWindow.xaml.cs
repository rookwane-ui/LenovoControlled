using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;

namespace LenovoController
{
    public partial class TrayMenuWindow : Wpf.Ui.Controls.FluentWindow, INotifyPropertyChanged
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
            // Show current power mode as subtitle
            try
            {
                var pm = new Features.PowerModeFeature();
                var state = pm.GetState();
                statusText.Text = state switch
                {
                    Features.PowerModeState.Quiet       => "Power saver",
                    Features.PowerModeState.Performance => "Best performance",
                    _                                    => "Balanced"
                };
            }
            catch
            {
                statusText.Text = "Running";
            }
        }

        /// <summary>Show the menu anchored to the tray icon position.</summary>
        public void ShowAtCursor()
        {
            // Get cursor position in device pixels
            var cursor = System.Windows.Forms.Cursor.Position;

            // Convert to WPF device-independent pixels
            var source = PresentationSource.FromVisual(this);
            double dpiX = 1.0, dpiY = 1.0;
            if (source?.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformFromDevice.M11;
                dpiY = source.CompositionTarget.TransformFromDevice.M22;
            }

            // Measure the window so we know its size
            Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Arrange(new Rect(DesiredSize));

            double w = ActualWidth  > 0 ? ActualWidth  : 240;
            double h = ActualHeight > 0 ? ActualHeight : 180;

            // Position above and to the left of the cursor, keep on screen
            var screen = SystemParameters.WorkArea;
            double left = cursor.X * dpiX - w + 8;
            double top  = cursor.Y * dpiY - h - 8;

            if (left < screen.Left) left = screen.Left + 8;
            if (top  < screen.Top)  top  = cursor.Y * dpiY + 8;

            Left = left;
            Top  = top;

            Show();
            Activate();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Close();
        }

        private void BtnOpen_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Close();
            _app.BringToFront();
        }

        private void BtnSettings_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Close();
            _app.OpenSettings();
        }

        private void BtnExit_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _app.Shutdown(0);
        }
    }
}
