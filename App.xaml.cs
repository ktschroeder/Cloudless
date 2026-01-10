using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Cloudless
{
    public partial class App : Application
    {
        private const string MutexName = "Cloudless.SingleInstance";
        private Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool isFirstInstance;
            _mutex = new Mutex(true, MutexName, out isFirstInstance);

            string? filePath = e.Args.FirstOrDefault();

            if (!isFirstInstance)
            {
                // Send args to primary instance and exit
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    SingleInstanceIpc.SendMessageToPrimary(filePath);
                }

                Shutdown();
                return;
            }

            // Primary instance startup
            SingleInstanceIpc.StartServer();

            SingleInstanceIpc.MessageReceived += OnIpcMessageReceived;

            base.OnStartup(e);

            var mainWindow = new MainWindow(filePath);
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SingleInstanceIpc.StopServer();
            _mutex?.ReleaseMutex();
            base.OnExit(e);
        }

        private void OnIpcMessageReceived(string message)
        {
            // Must marshal back to UI thread
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var win = new MainWindow(message);
                    win.Show();
                    win.Activate();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open file from IPC:\n{ex.Message}");
                }
            });
        }
    }
}
