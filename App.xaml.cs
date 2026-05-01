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
        private TrayIconService? _trayIconService;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Prevent WPF from shutting down when the last window closes so tray/background stays alive
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            bool isFirstInstance;
            _mutex = new Mutex(true, MutexName, out isFirstInstance);

            string? filePath = e.Args.FirstOrDefault();

            if (!isFirstInstance)
            {
                
                SingleInstanceIpc.SendMessageToPrimary(filePath ?? "");
                return;
            }

            SingleInstanceIpc.StartServer();
            SingleInstanceIpc.MessageReceived += OnIpcMessageReceived;

            _trayIconService = new TrayIconService();
            _trayIconService.Start();

            base.OnStartup(e);

            var mainWindow = new MainWindow(filePath, startUp: true);
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Dispose tray first to avoid tray callbacks during shutdown
            _trayIconService?.Dispose();
            _trayIconService = null;

            SingleInstanceIpc.StopServer();
            _mutex?.ReleaseMutex();

            base.OnExit(e);

            // Force process exit to ensure the debugger detaches/ends the session.
            // This is a safety measure: if a background thread/task remains it will be terminated here.
            try
            {
                Environment.Exit(0);
            }
            catch {}
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
                    MessageBox.Show($"Error: Failed to open file from IPC:\n{ex.Message}");  // rare MessageBox allowance
                }
            });
        }
    }
}