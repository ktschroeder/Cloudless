using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Threading;

namespace Cloudless;

internal sealed class MagnifierOverlayWindow : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmNcHitTest = 0x0084;
    private const int WmMouseActivate = 0x0021;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmMouseWheel = 0x020A;
    private const int WmPaint = 0x000F;
    private const int VkShift = 0x10;
    private const int VkMenu = 0x12;
    private const uint WsPopup = 0x80000000;
    private const uint WsChild = 0x40000000;
    private const uint WsVisible = 0x10000000;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExNoActivate = 0x08000000;
    private const uint WsExToolWindow = 0x00000080;
    private const uint LwaAlpha = 0x00000002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const int MagFilterModeExclude = 0;
    private const int MagnifierShowCursor = 0x0001;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const int ErrorClassAlreadyExists = 1410;
    private const int GrayBrush = 2;
    private const int LensBorderThickness = 3;
    private const int MinDiameter = 120;
    private const int MaxDiameter = 1800;
    private const double MinZoom = 1.0;
    private const double MaxZoom = 8.0;
    private const string HostWindowClassName = "Cloudless.MagnifierOverlay";
    private const string MagnifierClassName = "Magnifier";
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly WindowProcedure NativeWindowProcedure = NativeWndProc;
    private static MagnifierOverlayWindow? _activeWindow;

    private delegate IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    private readonly Dispatcher _dispatcher;
    private readonly LowLevelMouseProc _mouseHookProcedure;
    private readonly int _virtualScreenLeft;
    private readonly int _virtualScreenTop;
    private readonly int _virtualScreenWidth;
    private readonly int _virtualScreenHeight;
    private int _diameter = 560;
    private double _zoom = 2.0;
    private IntPtr _hostWindow;
    private IntPtr _magnifierWindow;
    private IntPtr _mouseHook;
    private NativePoint? _lastCursorPosition;
    private int _appliedDiameter;
    private double _appliedZoom;
    private bool _magnificationInitialized;
    private bool _suppressNextRightButtonUp;
    private bool _rightButtonHeld;
    private bool _isDisposed;

    private MagnifierOverlayWindow(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _mouseHookProcedure = MouseHookProc;
        _virtualScreenLeft = GetSystemMetrics(SmXVirtualScreen);
        _virtualScreenTop = GetSystemMetrics(SmYVirtualScreen);
        _virtualScreenWidth = GetSystemMetrics(SmCxVirtualScreen);
        _virtualScreenHeight = GetSystemMetrics(SmCyVirtualScreen);
        if (_virtualScreenWidth <= 0 || _virtualScreenHeight <= 0)
            throw new Win32Exception("Unable to determine virtual screen dimensions for the magnifier.");

        if (!MagInitialize())
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to initialize the Windows Magnification API.");
        _magnificationInitialized = true;

        try
        {
            IntPtr module = GetModuleHandle(null);
            EnsureWindowClassRegistered(module);
            if (!GetCursorPos(out NativePoint cursorPosition))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to determine the cursor position for the magnifier.");
            _hostWindow = CreateWindowEx(
                WsExLayered | WsExTransparent | WsExNoActivate | WsExToolWindow,
                HostWindowClassName,
                string.Empty,
                WsPopup,
                cursorPosition.X - _diameter / 2,
                cursorPosition.Y - _diameter / 2,
                _diameter,
                _diameter,
                IntPtr.Zero,
                IntPtr.Zero,
                module,
                IntPtr.Zero);
            if (_hostWindow == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create the magnifier overlay window.");

            if (!SetLayeredWindowAttributes(_hostWindow, 0, 255, LwaAlpha))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to set magnifier overlay opacity.");

            _magnifierWindow = CreateWindowEx(
                0,
                MagnifierClassName,
                string.Empty,
                WsChild | WsVisible | (uint)MagnifierShowCursor,
                LensBorderThickness,
                LensBorderThickness,
                _diameter - LensBorderThickness * 2,
                _diameter - LensBorderThickness * 2,
                _hostWindow,
                IntPtr.Zero,
                module,
                IntPtr.Zero);
            if (_magnifierWindow == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create the Windows magnifier control.");

            if (!MagSetWindowFilterList(_magnifierWindow, MagFilterModeExclude, 1, new[] { _hostWindow }))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to exclude the magnifier overlay from its own view.");

            UpdateLens(cursorPosition, throwOnFailure: true);

            _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseHookProcedure, module, 0);
            if (_mouseHook == IntPtr.Zero)
                Debug.WriteLine($"Unable to install magnifier mouse hook: {Marshal.GetLastWin32Error()}");

            CompositionTarget.Rendering += OnRendering;
        }
        catch
        {
            if (_hostWindow != IntPtr.Zero)
            {
                DestroyWindow(_hostWindow);
                _hostWindow = IntPtr.Zero;
            }

            if (_magnificationInitialized)
            {
                MagUninitialize();
                _magnificationInitialized = false;
            }

            throw;
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
                _activeWindow = new MagnifierOverlayWindow(Dispatcher.CurrentDispatcher);
            }
            catch (Win32Exception ex)
            {
                Debug.WriteLine($"Unable to activate magnifier mode: {ex.Message}");
            }

            return;
        }

        MagnifierOverlayWindow? activeWindow = _activeWindow;
        _activeWindow = null;
        activeWindow?.Dispose();
    }

    internal static double CalculateZoomAfterWheel(double zoom, int delta, bool fine, bool coarse)
    {
        if (delta == 0)
            return zoom;

        double scalePerNotch = fine == coarse ? 1.1 : fine ? 1.02 : 1.25;
        return Math.Clamp(zoom * Math.Pow(scalePerNotch, delta / 120.0), MinZoom, MaxZoom);
    }

    internal static int CalculateDiameterAfterWheel(int diameter, int delta, bool fine, bool coarse)
    {
        if (delta == 0)
            return diameter;

        double scalePerNotch = fine == coarse ? 1.1 : fine ? 1.02 : 1.25;
        return (int)Math.Clamp(Math.Round(diameter * Math.Pow(scalePerNotch, delta / 120.0)), MinDiameter, MaxDiameter);
    }

    internal static int GetSourceStart(int cursorCoordinate, int sourceLength) => cursorCoordinate - sourceLength / 2;

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_isDisposed)
            return;

        if (GetCursorPos(out NativePoint cursorPosition))
            UpdateLens(cursorPosition);

        if (_hostWindow != IntPtr.Zero)
            SetWindowPos(_hostWindow, HwndTopmost, 0, 0, 0, 0, SwpNoActivate | SwpNoMove | SwpNoSize);
    }

    private void QueueWheelChange(int delta, bool resize, bool fine, bool coarse)
    {
        if (_isDisposed)
            return;

        _dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_isDisposed)
                return;

            if (resize)
            {
                _diameter = CalculateDiameterAfterWheel(_diameter, delta, fine, coarse);
            }
            else
            {
                _zoom = CalculateZoomAfterWheel(_zoom, delta, fine, coarse);
            }

            if (_lastCursorPosition is NativePoint cursorPosition)
                UpdateLens(cursorPosition);
        }));
    }

    private void UpdateLens(NativePoint cursorPosition, bool throwOnFailure = false)
    {
        if (_isDisposed)
            return;

        bool diameterChanged = _appliedDiameter != _diameter;
        bool positionChanged = _lastCursorPosition is not NativePoint lastPosition ||
            lastPosition.X != cursorPosition.X || lastPosition.Y != cursorPosition.Y;
        int left = cursorPosition.X - _diameter / 2;
        int top = cursorPosition.Y - _diameter / 2;
        if ((positionChanged || diameterChanged) &&
            !SetWindowPos(_hostWindow, HwndTopmost, left, top, _diameter, _diameter, SwpNoActivate | SwpShowWindow))
        {
            HandleUpdateFailure(throwOnFailure, "Unable to position the magnifier overlay.");
            return;
        }

        if (diameterChanged)
        {
            if (!SetWindowPos(_magnifierWindow, IntPtr.Zero, LensBorderThickness, LensBorderThickness,
                    _diameter - LensBorderThickness * 2, _diameter - LensBorderThickness * 2, SwpNoActivate | SwpNoZOrder) ||
                !SetCircularMagnifierRegion() ||
                !SetCircularWindowRegion())
            {
                HandleUpdateFailure(throwOnFailure, "Unable to resize the magnifier lens.");
                return;
            }

            _appliedDiameter = _diameter;
        }

        if (diameterChanged || _appliedZoom != _zoom)
        {
            if (!SetMagnificationTransform())
            {
                HandleUpdateFailure(throwOnFailure, "Unable to apply the magnifier zoom transform.");
                return;
            }

            _appliedZoom = _zoom;
        }

        int sourceWidth = Math.Max(1, (int)Math.Round((_diameter - LensBorderThickness * 2) / _zoom));
        sourceWidth = Math.Min(sourceWidth, Math.Min(_virtualScreenWidth, _virtualScreenHeight));
        int sourceHeight = sourceWidth;
        int sourceLeft = GetSourceStart(cursorPosition.X, sourceWidth);
        int sourceTop = GetSourceStart(cursorPosition.Y, sourceHeight);
        NativeRect source = new(sourceLeft, sourceTop, sourceLeft + sourceWidth, sourceTop + sourceHeight);
        if (!MagSetWindowSource(_magnifierWindow, source))
        {
            HandleUpdateFailure(throwOnFailure, "Unable to update the magnifier source rectangle.");
            return;
        }

        _lastCursorPosition = cursorPosition;
    }

    private bool SetMagnificationTransform()
    {
        float zoom = (float)_zoom;
        NativeTransform transform = new()
        {
            M11 = zoom,
            M22 = zoom,
            M33 = 1f
        };
        return MagSetWindowTransform(_magnifierWindow, ref transform);
    }

    private bool SetCircularWindowRegion()
    {
        IntPtr region = CreateEllipticRgn(0, 0, _diameter, _diameter);
        if (region == IntPtr.Zero)
            return false;

        if (SetWindowRgn(_hostWindow, region, true) != 0)
            return true;

        DeleteObject(region);
        return false;
    }

    private bool SetCircularMagnifierRegion()
    {
        int contentDiameter = _diameter - LensBorderThickness * 2;
        IntPtr region = CreateEllipticRgn(0, 0, contentDiameter, contentDiameter);
        if (region == IntPtr.Zero)
            return false;

        if (SetWindowRgn(_magnifierWindow, region, true) != 0)
            return true;

        DeleteObject(region);
        return false;
    }

    private void HandleUpdateFailure(bool throwOnFailure, string message)
    {
        int error = Marshal.GetLastWin32Error();
        if (throwOnFailure)
            throw new Win32Exception(error, message);

        Debug.WriteLine($"{message} Win32 error: {error}");
    }

    private IntPtr MouseHookProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (_isDisposed)
            return CallNextHookEx(_mouseHook, code, wParam, lParam);

        if (code >= 0)
        {
            int message = wParam.ToInt32();
            if (message == WmRButtonDown)
            {
                _rightButtonHeld = true;
                _suppressNextRightButtonUp = false;
            }
            else if (message == WmRButtonUp)
            {
                _rightButtonHeld = false;
                if (_suppressNextRightButtonUp)
                {
                    _suppressNextRightButtonUp = false;
                    return new IntPtr(1);
                }
            }
            else if (message == WmMouseWheel)
            {
                MouseHookData data = Marshal.PtrToStructure<MouseHookData>(lParam);
                int delta = unchecked((short)((data.MouseData >> 16) & 0xFFFF));
                bool resize = _rightButtonHeld;
                bool fine = (GetAsyncKeyState(VkMenu) & 0x8000) != 0;
                bool coarse = (GetAsyncKeyState(VkShift) & 0x8000) != 0;
                if (resize)
                    _suppressNextRightButtonUp = true;
                QueueWheelChange(delta, resize, fine, coarse);
                return new IntPtr(1);
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
            BackgroundBrush = GetStockObject(GrayBrush),
            ClassName = HostWindowClassName
        };

        if (RegisterClass(ref windowClass) == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != ErrorClassAlreadyExists)
                throw new Win32Exception(error, "Unable to register the magnifier overlay window class.");
        }

    }

    private static IntPtr NativeWndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmNcHitTest)
            return new IntPtr(-1);
        if (message == WmMouseActivate)
            return new IntPtr(3);
        if (message == WmPaint)
        {
            IntPtr deviceContext = BeginPaint(hwnd, out PaintStruct paint);
            GetClientRect(hwnd, out NativeRect clientRectangle);
            FillRect(deviceContext, ref clientRectangle, GetStockObject(GrayBrush));
            EndPaint(hwnd, ref paint);
            return IntPtr.Zero;
        }
        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        CompositionTarget.Rendering -= OnRendering;
        if (_mouseHook != IntPtr.Zero && !UnhookWindowsHookEx(_mouseHook))
            Debug.WriteLine($"Unable to remove magnifier mouse hook: {Marshal.GetLastWin32Error()}");
        _mouseHook = IntPtr.Zero;
        if (_hostWindow != IntPtr.Zero && !DestroyWindow(_hostWindow))
            Debug.WriteLine($"Unable to destroy magnifier overlay window: {Marshal.GetLastWin32Error()}");
        _hostWindow = IntPtr.Zero;
        if (_magnificationInitialized)
        {
            MagUninitialize();
            _magnificationInitialized = false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeTransform
    {
        internal float M11;
        internal float M12;
        internal float M13;
        internal float M21;
        internal float M22;
        internal float M23;
        internal float M31;
        internal float M32;
        internal float M33;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;

        internal NativeRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
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

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        internal IntPtr DeviceContext;
        internal int Erase;
        internal NativeRect PaintRectangle;
        internal int Restore;
        internal int IncrementalUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        internal byte[] Reserved;
    }

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagInitialize();

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagUninitialize();

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagSetWindowSource(IntPtr magnifierWindow, NativeRect source);

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagSetWindowTransform(IntPtr magnifierWindow, ref NativeTransform transform);

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagSetWindowFilterList(IntPtr magnifierWindow, int filterMode, int count, IntPtr[] windows);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string windowName, uint style, int x, int y,
        int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

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
    private static extern IntPtr BeginPaint(IntPtr hwnd, out PaintStruct paint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndPaint(IntPtr hwnd, ref PaintStruct paint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int FillRect(IntPtr deviceContext, ref NativeRect rectangle, IntPtr brush);

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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr graphicsObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateEllipticRgn(int left, int top, int right, int bottom);
}
