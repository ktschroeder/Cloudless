using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Cloudless;

internal sealed class MultifunctionalOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;
    private const int WsExNoActivate = 0x08000000;
    private const uint GwHwndPrev = 3;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoOwnerZOrder = 0x0200;

    private readonly MainWindow _owner;
    private readonly DispatcherTimer _zOrderTimer;
    private readonly Border _selectionVisual;
    private readonly Rectangle _cropBorderVisual;
    private bool _isSelected;
    private bool _isCropMode;
    private bool _isClosed;

    internal MultifunctionalOverlayWindow(MainWindow owner)
    {
        _owner = owner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = false;
        Focusable = false;
        IsHitTestVisible = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        (_selectionVisual, _cropBorderVisual, Content) = CreateOverlayContent();

        _zOrderTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        _zOrderTimer.Tick += (_, _) =>
        {
            _zOrderTimer.Stop();
            RestoreZOrder();
        };

        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        _owner.LocationChanged += OnOwnerLocationOrSizeChanged;
        _owner.SizeChanged += OnOwnerLocationOrSizeChanged;
        _owner.StateChanged += OnOwnerStateChanged;
        _owner.IsVisibleChanged += OnOwnerIsVisibleChanged;
        _owner.Activated += OnOwnerActivated;
    }

    internal void SetSelected(bool isSelected)
    {
        if (_isClosed)
            return;

        _isSelected = isSelected;
        RefreshVisibility();
    }

    internal void SetCropMode(bool isCropMode)
    {
        if (_isClosed)
            return;

        _isCropMode = isCropMode;
        RefreshVisibility();
    }

    internal void EnsureZOrderAboveOwner()
    {
        if ((_isSelected || _isCropMode) && IsVisible)
            _zOrderTimer.Start();
    }

    private static (Border SelectionVisual, Rectangle CropBorderVisual, Grid Content) CreateOverlayContent()
    {
        var checkerboard = new DrawingGroup();
        var blueBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x34, 0x7F, 0xFF));
        blueBrush.Freeze();
        checkerboard.Children.Add(new GeometryDrawing(blueBrush, null,
            Geometry.Parse("M0,0 L10,0 10,10 0,10 Z")));
        checkerboard.Children.Add(new GeometryDrawing(blueBrush, null,
            Geometry.Parse("M10,10 L20,10 20,20 10,20 Z")));
        checkerboard.Freeze();

        var brush = new DrawingBrush(checkerboard)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 20, 20),
            ViewportUnits = BrushMappingMode.Absolute,
            Opacity = 0.45
        };
        brush.Freeze();

        var selectionVisual = new Border
        {
            Background = brush,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        var cropBorderVisual = new Rectangle
        {
            Stroke = Brushes.Red,
            StrokeThickness = 2,
            Margin = new Thickness(0),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        var content = new Grid { IsHitTestVisible = false };
        content.Children.Add(selectionVisual);
        content.Children.Add(cropBorderVisual);
        return (selectionVisual, cropBorderVisual, content);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GwlExStyle);
        exStyle = (exStyle | WsExToolWindow | WsExTransparent | WsExNoActivate) & ~WsExAppWindow;
        SetWindowLong(hwnd, GwlExStyle, exStyle);
    }

    private void OnOwnerLocationOrSizeChanged(object? sender, EventArgs e)
    {
        if ((_isSelected || _isCropMode) && _owner.IsVisible && _owner.WindowState != WindowState.Minimized)
            AlignToOwner();
    }

    private void OnOwnerStateChanged(object? sender, EventArgs e)
    {
        if (_owner.WindowState == WindowState.Minimized)
        {
            Hide();
            return;
        }

        RefreshVisibility();
    }

    private void OnOwnerIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_owner.IsVisible)
            Hide();
        else
            RefreshVisibility();
    }

    private void OnOwnerActivated(object? sender, EventArgs e) => EnsureZOrderAboveOwner();

    private void OnClosed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _zOrderTimer.Stop();
        _owner.LocationChanged -= OnOwnerLocationOrSizeChanged;
        _owner.SizeChanged -= OnOwnerLocationOrSizeChanged;
        _owner.StateChanged -= OnOwnerStateChanged;
        _owner.IsVisibleChanged -= OnOwnerIsVisibleChanged;
        _owner.Activated -= OnOwnerActivated;
    }

    private void AlignToOwner()
    {
        Left = _owner.Left;
        Top = _owner.Top;
        Width = Math.Max(1, _owner.ActualWidth);
        Height = Math.Max(1, _owner.ActualHeight);
    }

    private void RefreshVisibility()
    {
        _selectionVisual.Visibility = _isSelected ? Visibility.Visible : Visibility.Collapsed;
        _cropBorderVisual.Visibility = _isCropMode ? Visibility.Visible : Visibility.Collapsed;

        if ((!_isSelected && !_isCropMode) || !_owner.IsVisible || _owner.WindowState == WindowState.Minimized)
        {
            Hide();
            return;
        }

        if (Owner != _owner)
            Owner = _owner;

        AlignToOwner();
        if (!IsVisible)
            Show();
        EnsureZOrderAboveOwner();
    }

    private void RestoreZOrder()
    {
        if ((!_isSelected && !_isCropMode) || !IsVisible)
            return;

        IntPtr overlayHwnd = new WindowInteropHelper(this).Handle;
        IntPtr ownerHwnd = new WindowInteropHelper(_owner).Handle;
        if (overlayHwnd == IntPtr.Zero || ownerHwnd == IntPtr.Zero)
            return;

        IntPtr windowAboveOwner = GetWindow(ownerHwnd, GwHwndPrev);
        if (windowAboveOwner == overlayHwnd)
            return;

        SetWindowPos(
            overlayHwnd,
            windowAboveOwner,
            0,
            0,
            0,
            0,
            SwpNoSize | SwpNoMove | SwpNoActivate | SwpNoOwnerZOrder);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint command);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
