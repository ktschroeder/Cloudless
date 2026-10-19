using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Cloudless;

internal sealed class SpotlightOverlayWindow : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmNcHitTest = 0x0084;
    private const int WmMouseActivate = 0x0021;
    private const int WmMouseMove = 0x0200;
    private const int WmMouseWheel = 0x020A;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const uint WsPopup = 0x80000000;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExNoActivate = 0x08000000;
    private const uint WsExToolWindow = 0x00000080;
    private const uint LwaAlpha = 0x00000002;
    private const int RgnDiff = 4;
    private const int BlackBrush = 4;
    private const int ErrorClassAlreadyExists = 1410;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const byte OverlayAlpha = 180;
    private const double CursorMargin = 12;
    private const string WindowClassName = "Cloudless.SpotlightOverlay";
    private static readonly IntPtr HwndTopmost = new(-1);
    private delegate IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    private static readonly WindowProcedure NativeWindowProcedure = NativeWndProc;
    private static int _diameter = 300;
    private static SpotlightOverlayWindow? _activeWindow;

    private readonly Dispatcher _dispatcher;
    private readonly LowLevelMouseProc _mouseHookProcedure;
    private readonly object _cursorPositionLock = new();
    private readonly int _left;
    private readonly int _top;
    private readonly int _width;
    private readonly int _height;
    private IntPtr _hwnd;
    private IntPtr _mouseHook;
    private DispatcherTimer? _cursorTimer;
    private NativePoint? _lastCursorPosition;
    private NativePoint _pendingCursorPosition;
    private bool _cursorUpdatePending;

    private SpotlightOverlayWindow(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _mouseHookProcedure = MouseHookProc;
        _left = GetSystemMetrics(SmXVirtualScreen) - (int)CursorMargin;
        _top = GetSystemMetrics(SmYVirtualScreen) - (int)CursorMargin;
        _width = GetSystemMetrics(SmCxVirtualScreen) + (int)CursorMargin * 2;
        _height = GetSystemMetrics(SmCyVirtualScreen) + (int)CursorMargin * 2;
        if (_width <= 0 || _height <= 0)
            throw new Win32Exception("Unable to determine virtual screen dimensions for the spotlight overlay.");

        IntPtr module = GetModuleHandle(null);
        EnsureWindowClassRegistered(module);
        _hwnd = CreateWindowEx(
            WsExLayered | WsExTransparent | WsExNoActivate | WsExToolWindow,
            WindowClassName,
            string.Empty,
            WsPopup,
            _left,
            _top,
            _width,
            _height,
            IntPtr.Zero,
            IntPtr.Zero,
            module,
            IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create the spotlight overlay window.");

        try
        {
            if (!SetLayeredWindowAttributes(_hwnd, 0, OverlayAlpha, LwaAlpha))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to set spotlight overlay opacity.");

            if (!GetCursorPos(out NativePoint cursorPosition))
                cursorPosition = new NativePoint { X = _left + _width / 2, Y = _top + _height / 2 };
            SetSpotlightRegion(cursorPosition, throwOnFailure: true);
            if (!SetWindowPos(_hwnd, HwndTopmost, _left, _top, _width, _height, SwpNoActivate | SwpShowWindow))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to show the spotlight overlay window.");

            _lastCursorPosition = cursorPosition;
        }
        catch
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
            throw;
        }

        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseHookProcedure, module, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            Debug.WriteLine($"Unable to install spotlight mouse hook: {Marshal.GetLastWin32Error()}");
            _cursorTimer = new DispatcherTimer(DispatcherPriority.Render, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _cursorTimer.Tick += (_, _) => UpdateSpotlightPosition();
            _cursorTimer.Start();
        }
    }

    internal static void SetActive(bool active)
    {
        if (active)
        {
            if (_activeWindow != null)
                return;

            try
            {
                _activeWindow = new SpotlightOverlayWindow(Dispatcher.CurrentDispatcher);
            }
            catch (Win32Exception ex)
            {
                Debug.WriteLine($"Unable to activate spotlight mode: {ex.Message}");
            }

            return;
        }

        SpotlightOverlayWindow? activeWindow = _activeWindow;
        _activeWindow = null;
        activeWindow?.Dispose();
    }

    private void UpdateSpotlightPosition()
    {
        if (GetCursorPos(out NativePoint cursorPosition))
            UpdateSpotlightPosition(cursorPosition);
    }

    private void UpdateSpotlightPosition(NativePoint cursorPosition)
    {
        if (_lastCursorPosition is NativePoint lastPosition &&
            lastPosition.X == cursorPosition.X && lastPosition.Y == cursorPosition.Y)
            return;

        _lastCursorPosition = cursorPosition;
        SetSpotlightRegion(cursorPosition);
    }

    private void SetSpotlightRegion(NativePoint cursorPosition, bool throwOnFailure = false)
    {
        int centerX = cursorPosition.X - _left;
        int centerY = cursorPosition.Y - _top;
        int radius = _diameter / 2;
        IntPtr region = CreateRectRgn(0, 0, _width, _height);
        IntPtr spotlightRegion = CreateEllipticRgn(centerX - radius, centerY - radius, centerX + radius + 1, centerY + radius + 1);
        if (region == IntPtr.Zero || spotlightRegion == IntPtr.Zero)
        {
            if (region != IntPtr.Zero)
                DeleteObject(region);
            if (spotlightRegion != IntPtr.Zero)
                DeleteObject(spotlightRegion);
            HandleRegionFailure(throwOnFailure, "Unable to create the spotlight window region.");
            return;
        }

        int combineResult = CombineRgn(region, region, spotlightRegion, RgnDiff);
        DeleteObject(spotlightRegion);
        if (combineResult == 0)
        {
            DeleteObject(region);
            HandleRegionFailure(throwOnFailure, "Unable to combine the spotlight window region.");
            return;
        }

        if (SetWindowRgn(_hwnd, region, true) == 0)
        {
            DeleteObject(region);
            HandleRegionFailure(throwOnFailure, "Unable to apply the spotlight window region.");
            return;
        }

        _lastCursorPosition = cursorPosition;
    }

    private static void HandleRegionFailure(bool throwOnFailure, string message)
    {
        int error = Marshal.GetLastWin32Error();
        if (throwOnFailure)
            throw new Win32Exception(error, message);

        Debug.WriteLine($"{message} Win32 error: {error}");
    }

    private void UpdateSpotlightSize(int delta, bool fine, bool coarse)
    {
        _diameter = CalculateDiameterAfterWheel(_diameter, delta, fine, coarse);
        if (_lastCursorPosition is NativePoint cursorPosition)
            SetSpotlightRegion(cursorPosition);
    }

    private void QueueSpotlightPosition(NativePoint cursorPosition)
    {
        lock (_cursorPositionLock)
        {
            _pendingCursorPosition = cursorPosition;
            if (_cursorUpdatePending)
                return;

            _cursorUpdatePending = true;
        }

        _dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            NativePoint latestPosition;
            lock (_cursorPositionLock)
            {
                latestPosition = _pendingCursorPosition;
                _cursorUpdatePending = false;
            }

            UpdateSpotlightPosition(latestPosition);
        }));
    }

    internal static int CalculateDiameterAfterWheel(int diameter, int delta, bool fine, bool coarse)
    {
        if (delta == 0)
            return diameter;

        double scalePerNotch = fine == coarse ? 1.1 : fine ? 1.02 : 1.25;
        return (int)Math.Clamp(
            Math.Round(diameter * Math.Pow(scalePerNotch, delta / 120.0)),
            10,
            4000);
    }

    private IntPtr MouseHookProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            int message = wParam.ToInt32();
            if (message == WmMouseMove)
            {
                MouseHookData data = Marshal.PtrToStructure<MouseHookData>(lParam);
                QueueSpotlightPosition(data.Point);
            }
            else if (message == WmMouseWheel)
            {
                MouseHookData data = Marshal.PtrToStructure<MouseHookData>(lParam);
                int delta = unchecked((short)((data.MouseData >> 16) & 0xFFFF));
                bool control = (GetAsyncKeyState(VkControl) & 0x8000) != 0;
                bool fine = (GetAsyncKeyState(VkMenu) & 0x8000) != 0;
                bool coarse = (GetAsyncKeyState(VkShift) & 0x8000) != 0;
                if (!control)
                    _dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => UpdateSpotlightSize(delta, fine, coarse)));
            }
        }

        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private static void EnsureWindowClassRegistered(IntPtr module)
    {
        WindowClass windowClass = new()
        {
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(NativeWindowProcedure),
            Instance = module,
            BackgroundBrush = GetStockObject(BlackBrush),
            ClassName = WindowClassName
        };

        if (RegisterClass(ref windowClass) == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != ErrorClassAlreadyExists)
                throw new Win32Exception(error, "Unable to register the spotlight overlay window class.");
        }
    }

    private static IntPtr NativeWndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmNcHitTest)
            return new IntPtr(-1);
        if (message == WmMouseActivate)
            return new IntPtr(3);

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        _cursorTimer?.Stop();
        if (_mouseHook != IntPtr.Zero && !UnhookWindowsHookEx(_mouseHook))
            Debug.WriteLine($"Unable to remove spotlight mouse hook: {Marshal.GetLastWin32Error()}");
        _mouseHook = IntPtr.Zero;
        if (_hwnd != IntPtr.Zero && !DestroyWindow(_hwnd))
            Debug.WriteLine($"Unable to destroy spotlight overlay window: {Marshal.GetLastWin32Error()}");
        _hwnd = IntPtr.Zero;
    }

    private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        internal NativePoint Point;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        internal uint Style;
        internal IntPtr WindowProcedure;
        internal int ClassExtra;
        internal int WindowExtra;
        internal IntPtr Instance;
        internal IntPtr Icon;
        internal IntPtr Cursor;
        internal IntPtr BackgroundBrush;
        internal IntPtr MenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        internal string ClassName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterClassW", SetLastError = true)]
    private static extern ushort RegisterClass(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DefWindowProcW")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelMouseProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr GetStockObject(int objectIndex);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateEllipticRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int CombineRgn(IntPtr destination, IntPtr source1, IntPtr source2, int mode);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr graphicsObject);
}
