using System;
using System.Windows;
using Microsoft.Win32.SafeHandles;

namespace LenovoController.Providers
{
    /// <summary>
    /// Provides a cached, thread-safe handle to the Lenovo Energy Management driver.
    /// </summary>
    public static class DriverProvider
    {
        private static readonly object _lock = new object();
        private static SafeFileHandle _energyDriver;

        public static bool ErrorShown { get; private set; }

        public static SafeFileHandle EnergyDriver
        {
            get
            {
                if (_energyDriver != null && !_energyDriver.IsInvalid && !_energyDriver.IsClosed)
                    return _energyDriver;

                lock (_lock)
                {
                    // Double-checked locking
                    if (_energyDriver != null && !_energyDriver.IsInvalid && !_energyDriver.IsClosed)
                        return _energyDriver;

                    var fileHandle = Native.CreateFileW(
                        "\\\\.\\EnergyDrv",
                        0xC0000000,
                        3u,
                        IntPtr.Zero,
                        3u,
                        0x80,
                        IntPtr.Zero);

                    if (fileHandle == new IntPtr(-1))
                    {
                        if (!ErrorShown)
                        {
                            ErrorShown = true;

                            string msg = App.Instance?.Settings?.Culture switch
                            {
                                "RU" => "Драйвер Lenovo Energy Management не найден.\nУстановите драйвер и повторите попытку.",
                                "UA" => "Драйвер Lenovo Energy Management не знайдено.\nВстановіть драйвер і спробуйте ще раз.",
                                _    => "Lenovo Energy Management driver was not found.\nPlease install the driver and try again."
                            };

                            MessageBox.Show(msg, "Lenovo Controller",
                                MessageBoxButton.OK, MessageBoxImage.Error);

                            App.Instance?.Shutdown(-1);
                        }

                        return null;
                    }

                    _energyDriver = new SafeFileHandle(fileHandle, ownsHandle: true);
                }

                return _energyDriver;
            }
        }
    }
}
