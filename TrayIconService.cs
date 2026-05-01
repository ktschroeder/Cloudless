using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Cloudless
{
    internal sealed class TrayIconService : IDisposable
    {
        // Tray callback message (choose WM_APP + something)
        private const int WM_APP = 0x8000;
        private const int WM_TRAY_CALLBACK = WM_APP + 1;

        // Mouse messages
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_CONTEXTMENU = 0x007B;

        // Shell_NotifyIcon messages
        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;

        // NOTIFYICONDATA flags
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;

        // Native menu flags
        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_LEFTALIGN = 0x0000;
        private const uint TPM_RIGHTBUTTON = 0x0002;

        // Command IDs for menu items
        private const uint CMD_OPEN = 1001;
        private const uint CMD_CLOSE_ALL = 1002;
        private const uint CMD_SHUTDOWN = 1003;

        private readonly HwndSource _hwndSource;
        private readonly uint _uId = 1;
        private IntPtr _hIcon = IntPtr.Zero;
        private bool _isAdded;

        public TrayIconService()
        {
            // Create a message-only HwndSource to receive tray callbacks
            var parameters = new HwndSourceParameters("CloudlessTrayMessageWindow")
            {
                WindowStyle = 0,
                Width = 0,
                Height = 0,
                ParentWindow = new IntPtr(-3),  // HWND_MESSAGE (message-only window)
                UsesPerPixelOpacity = false
            };

            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);

            // Try to obtain an HICON from the executable, fallback to application icon
            _hIcon = GetAppIconHandle() ?? LoadIcon(GetModuleHandle(null), IDI_APPLICATION);

            // Register the icon
            AddIcon("Cloudless");
        }

        public void Start()
        {
            // No-op; icon added in ctor.
        }

        private void AddIcon(string tooltip)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwndSource.Handle,
                uID = _uId,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAY_CALLBACK,
                hIcon = _hIcon,
                szTip = tooltip
            };

            _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
        }

        private void RemoveIcon()
        {
            if (!_isAdded) return;

            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwndSource.Handle,
                uID = _uId
            };

            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _isAdded = false;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAY_CALLBACK)
            {
                int mouseMsg = lParam.ToInt32();
                switch (mouseMsg)
                {
                    case WM_LBUTTONDBLCLK:
                        DispatcherInvokeSafe(ShowOrCreateMainWindow);
                        handled = true;
                        break;
                    case WM_RBUTTONUP:
                    case WM_CONTEXTMENU:
                        DispatcherInvokeSafe(ShowNativeContextMenu);
                        handled = true;
                        break;
                }
            }

            return IntPtr.Zero;
        }

        private void ShowOrCreateMainWindow()
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                var main = app.MainWindow;
                if (main != null)
                {
                    if (main.WindowState == WindowState.Minimized)
                        main.WindowState = WindowState.Normal;

                    main.Show();
                    main.Activate();
                }
                else
                {
                    var win = new MainWindow(null);
                    app.MainWindow = win;
                    win.Show();
                    win.Activate();
                }
            }
            catch
            {
                // swallow
            }
        }

        private void CloseAllCloudlessWindows()
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                var windows = app.Windows.Cast<Window>().ToList();
                foreach (var w in windows)
                {
                    if (w is MainWindow || w.GetType().Namespace == typeof(MainWindow).Namespace)
                    {
                        w.Close();
                    }
                }
            }
            catch
            {
                // swallow
            }
        }

        private void ShutdownCloudless()
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                CloseAllCloudlessWindows();

                // Trigger normal shutdown (OnExit will run and cleanup)
                app.Shutdown();
            }
            catch
            {
                // swallow
            }
        }

        private void ShowNativeContextMenu()
        {
            IntPtr hMenu = IntPtr.Zero;
            try
            {
                hMenu = CreatePopupMenu();
                if (hMenu == IntPtr.Zero) return;

                AppendMenu(hMenu, MF_STRING, new UIntPtr(CMD_OPEN), "Open Cloudless");
                AppendMenu(hMenu, MF_STRING, new UIntPtr(CMD_CLOSE_ALL), "Close all Cloudless windows");
                AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, null);
                AppendMenu(hMenu, MF_STRING, new UIntPtr(CMD_SHUTDOWN), "Shutdown Cloudless");

                if (!GetCursorPos(out POINT pt)) return;

                // TrackPopupMenuEx returns the command id selected
                uint cmd = TrackPopupMenuEx(
                    hMenu,
                    TPM_RETURNCMD | TPM_LEFTALIGN | TPM_RIGHTBUTTON,
                    pt.X,
                    pt.Y,
                    _hwndSource.Handle,
                    IntPtr.Zero);

                if (cmd == 0) return;

                switch (cmd)
                {
                    case CMD_OPEN:
                        DispatcherInvokeSafe(ShowOrCreateMainWindow);
                        break;
                    case CMD_CLOSE_ALL:
                        DispatcherInvokeSafe(CloseAllCloudlessWindows);
                        break;
                    case CMD_SHUTDOWN:
                        DispatcherInvokeSafe(ShutdownCloudless);
                        break;
                }
            }
            finally
            {
                if (hMenu != IntPtr.Zero)
                {
                    DestroyMenu(hMenu);
                }
            }
        }

        private void DispatcherInvokeSafe(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
            }
        }

        public void Dispose()
        {
            RemoveIcon();

            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();

            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }
        }

        private static IntPtr? GetAppIconHandle()
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrEmpty(exe)) return null;

                IntPtr large = IntPtr.Zero;
                IntPtr small = IntPtr.Zero;
                uint got = ExtractIconEx(exe, 0, out large, out small, 1);
                if (got > 0)
                {
                    if (small != IntPtr.Zero) return small;
                    if (large != IntPtr.Zero) return large;
                }
            }
            catch { }

            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA pnid);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private static readonly IntPtr IDI_APPLICATION = new IntPtr(0x7F00);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
    }
}