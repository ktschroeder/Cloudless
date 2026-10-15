using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Cloudless;

internal sealed class MarqueeSelectionOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;
    private const int WsExNoActivate = 0x08000000;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    private readonly double _scaleX;
    private readonly double _scaleY;
    private readonly int _virtualLeft;
    private readonly int _virtualTop;
    private readonly Canvas _canvas;
    private readonly Rectangle _rectangle;

    internal MarqueeSelectionOverlayWindow(MainWindow owner)
    {
        Owner = owner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        Matrix deviceTransform = PresentationSource.FromVisual(owner)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        _scaleX = deviceTransform.M11 > 0 ? deviceTransform.M11 : 1;
        _scaleY = deviceTransform.M22 > 0 ? deviceTransform.M22 : 1;
        _virtualLeft = GetSystemMetrics(SmXVirtualScreen);
        _virtualTop = GetSystemMetrics(SmYVirtualScreen);
        int virtualWidth = Math.Max(1, GetSystemMetrics(SmCxVirtualScreen));
        int virtualHeight = Math.Max(1, GetSystemMetrics(SmCyVirtualScreen));

        Left = _virtualLeft / _scaleX;
        Top = _virtualTop / _scaleY;
        Width = virtualWidth / _scaleX;
        Height = virtualHeight / _scaleY;

        _rectangle = new Rectangle
        {
            Fill = Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.FromRgb(0x34, 0x7F, 0xFF)),
            StrokeThickness = 2,
            IsHitTestVisible = false
        };
        _canvas = new Canvas { IsHitTestVisible = false };
        _canvas.Children.Add(_rectangle);
        Content = _canvas;

        SourceInitialized += OnSourceInitialized;
        owner.Closed += (_, _) => Close();
    }

    internal void SetRectangle(Rect screenRectangle)
    {
        double left = (screenRectangle.Left - _virtualLeft) / _scaleX;
        double top = (screenRectangle.Top - _virtualTop) / _scaleY;
        _rectangle.Width = screenRectangle.Width / _scaleX;
        _rectangle.Height = screenRectangle.Height / _scaleY;
        Canvas.SetLeft(_rectangle, left);
        Canvas.SetTop(_rectangle, top);

        if (!IsVisible)
            Show();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GwlExStyle);
        exStyle = (exStyle | WsExToolWindow | WsExTransparent | WsExNoActivate) & ~WsExAppWindow;
        SetWindowLong(hwnd, GwlExStyle, exStyle);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
