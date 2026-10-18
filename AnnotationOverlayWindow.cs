using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Drawing.Drawing2D;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingBrush = System.Drawing.SolidBrush;
using DrawingColor = System.Drawing.Color;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPen = System.Drawing.Pen;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Cloudless;

internal sealed class AnnotationOverlayWindow : Window
{
    private static readonly Cursor EraserCursor = CreateEraserCursor();
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
    private readonly Action _openControls;
    private readonly Canvas _canvas = new();
    private readonly List<Stroke> _strokes = new();
    private readonly DispatcherTimer _rightHoldTimer;
    private readonly DispatcherTimer _zOrderTimer;
    private Brush _strokeBrush = Brushes.Red;
    private double _strokeSize = 4;
    private bool _annotationMode;
    private bool _eraseMode;
    private bool _isErasing;
    private bool _controlsVisible;
    private bool _sourceInitialized;
    private Polyline? _activeStroke;

    internal AnnotationOverlayWindow(MainWindow owner, Action openControls)
    {
        _owner = owner;
        _openControls = openControls;
        Owner = owner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Content = new Grid { Background = Brushes.Transparent, Children = { _canvas } };

        _rightHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _rightHoldTimer.Tick += RightHoldTimer_Tick;
        _zOrderTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        _zOrderTimer.Tick += (_, _) =>
        {
            _zOrderTimer.Stop();
            RestoreZOrder();
        };

        owner.LocationChanged += Owner_LayoutChanged;
        owner.SizeChanged += Owner_LayoutChanged;
        owner.StateChanged += Owner_StateChanged;
        owner.IsVisibleChanged += Owner_IsVisibleChanged;
        owner.Activated += Owner_Activated;
        owner.Closed += Owner_Closed;
        SourceInitialized += Overlay_SourceInitialized;
        Closed += Overlay_Closed;
    }

    internal bool IsEraseMode => _eraseMode;
    internal Cursor AnnotationCursor => _eraseMode ? EraserCursor : Cursors.Pen;

    internal void SetAnnotationMode(bool enabled)
    {
        _annotationMode = enabled;
        Cursor = enabled ? AnnotationCursor : Cursors.Arrow;
        if (!enabled)
        {
            _rightHoldTimer.Stop();
            _activeStroke = null;
            _isErasing = false;
            if (Mouse.Captured == this)
                Mouse.Capture(null);
        }
        UpdateHitTestingStyle();
        UpdateVisibility();
        if (enabled)
            AlignToOwner();
    }

    internal void SetColor(System.Windows.Media.Color color)
    {
        _strokeBrush = new SolidColorBrush(color);
        _eraseMode = false;
        _owner.RefreshGlobalMouseModeCursor();
    }

    internal void SetEraseMode(bool enabled)
    {
        _eraseMode = enabled;
        _owner.RefreshGlobalMouseModeCursor();
    }

    internal void SetControlsVisible(bool visible, AnnotationControlWindow? controls = null)
    {
        _controlsVisible = visible;
        if (visible)
        {
            _zOrderTimer.Stop();
            if (controls?.IsVisible == true)
            {
                IntPtr overlayHwnd = new WindowInteropHelper(this).Handle;
                IntPtr controlsHwnd = new WindowInteropHelper(controls).Handle;
                if (overlayHwnd != IntPtr.Zero && controlsHwnd != IntPtr.Zero)
                    SetWindowPos(overlayHwnd, controlsHwnd, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate | SwpNoOwnerZOrder);
            }
        }
        else
        {
            EnsureZOrderAboveOwner();
        }
    }

    internal void SetStrokeSize(double size) => _strokeSize = Math.Clamp(size, 1, 30);

    internal void BeginAnnotationInput(Point point)
    {
        if (!_annotationMode)
            return;

        if (_eraseMode)
        {
            EraseAt(point);
            _isErasing = true;
            return;
        }

        _activeStroke = new Polyline
        {
            Stroke = _strokeBrush,
            StrokeThickness = _strokeSize,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };
        Stroke stroke = new(_activeStroke);
        _activeStroke.Points.Add(point);
        _strokes.Add(stroke);
        _canvas.Children.Add(_activeStroke);
    }

    internal void ContinueAnnotationInput(Point point)
    {
        if (!_annotationMode)
            return;

        if (_isErasing)
            EraseAt(point);
        else if (_activeStroke != null)
            _activeStroke.Points.Add(point);
    }

    internal void EndAnnotationInput()
    {
        _activeStroke = null;
        _isErasing = false;
    }

    internal void StartRightHold() 
    {
        _rightHoldTimer.Stop();
        _rightHoldTimer.Start();
    }

    internal void StopRightHold() => _rightHoldTimer.Stop();

    internal void ClearAnnotations()
    {
        _strokes.Clear();
        _canvas.Children.Clear();
        _activeStroke = null;
        UpdateVisibility();
    }

    private void RightHoldTimer_Tick(object? sender, EventArgs e)
    {
        _rightHoldTimer.Stop();
        if (!_annotationMode || Mouse.RightButton != MouseButtonState.Pressed)
            return;
        _openControls();
    }

    private void EraseAt(Point point)
    {
        double radius = Math.Max(8, _strokeSize * 2);
        foreach (Stroke stroke in _strokes.ToArray())
        {
            if (IsNearStroke(stroke.Polyline.Points, point, radius))
            {
                _strokes.Remove(stroke);
                _canvas.Children.Remove(stroke.Polyline);
            }
        }
    }

    private static bool IsNearStroke(PointCollection points, Point point, double radius)
    {
        if (points.Any(existing => (existing - point).Length <= radius))
            return true;

        for (int index = 1; index < points.Count; index++)
        {
            Point start = points[index - 1];
            Vector segment = points[index] - start;
            double lengthSquared = segment.LengthSquared;
            if (lengthSquared == 0)
                continue;

            double projection = Math.Clamp(Vector.Multiply(point - start, segment) / lengthSquared, 0, 1);
            Point nearest = start + projection * segment;
            if ((nearest - point).Length <= radius)
                return true;
        }

        return false;
    }

    private void Owner_LayoutChanged(object? sender, EventArgs e) => AlignToOwner();

    private void Owner_Activated(object? sender, EventArgs e) => EnsureZOrderAboveOwner();

    private void Owner_StateChanged(object? sender, EventArgs e)
    {
        if (_owner.WindowState == WindowState.Minimized)
            Hide();
        else
            UpdateVisibility();
    }

    private void Owner_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateVisibility();

    private void Owner_Closed(object? sender, EventArgs e) => Close();

    private void AlignToOwner()
    {
        if (_owner.WindowState == WindowState.Minimized || !_owner.IsVisible)
            return;

        Left = _owner.Left;
        Top = _owner.Top;
        Width = Math.Max(1, _owner.ActualWidth);
        Height = Math.Max(1, _owner.ActualHeight);
    }

    private void UpdateVisibility()
    {
        if (_owner.IsVisible && _owner.WindowState != WindowState.Minimized && (_annotationMode || _strokes.Count > 0))
        {
            AlignToOwner();
            if (!IsVisible)
                Show();
            UpdateHitTestingStyle();
            EnsureZOrderAboveOwner();
        }
        else if (IsVisible)
        {
            Hide();
        }
    }

    private void UpdateHitTestingStyle()
    {
        if (!_sourceInitialized)
            return;

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GwlExStyle);
        exStyle = (exStyle | WsExToolWindow | WsExNoActivate) & ~WsExAppWindow;
        exStyle |= WsExTransparent;
        SetWindowLong(hwnd, GwlExStyle, exStyle);
    }

    private void Overlay_SourceInitialized(object? sender, EventArgs e)
    {
        _sourceInitialized = true;
        UpdateHitTestingStyle();
        AlignToOwner();
    }

    private void EnsureZOrderAboveOwner()
    {
        if (!_controlsVisible && IsVisible && _owner.IsVisible)
            _zOrderTimer.Start();
    }

    private void RestoreZOrder()
    {
        if (_controlsVisible || !IsVisible || !_owner.IsVisible)
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

    private void Overlay_Closed(object? sender, EventArgs e)
    {
        _rightHoldTimer.Stop();
        _zOrderTimer.Stop();
        _owner.LocationChanged -= Owner_LayoutChanged;
        _owner.SizeChanged -= Owner_LayoutChanged;
        _owner.StateChanged -= Owner_StateChanged;
        _owner.IsVisibleChanged -= Owner_IsVisibleChanged;
        _owner.Activated -= Owner_Activated;
        _owner.Closed -= Owner_Closed;
    }

    private static Cursor CreateEraserCursor()
    {
        using DrawingBitmap bitmap = new(32, 32, DrawingPixelFormat.Format32bppArgb);
        using (DrawingGraphics graphics = DrawingGraphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(DrawingColor.Transparent);
            graphics.TranslateTransform(16, 16);
            graphics.RotateTransform(-45);
            using DrawingBrush fill = new(DrawingColor.FromArgb(255, 238, 70, 170));
            using DrawingPen outline = new(DrawingColor.FromArgb(255, 45, 35, 45), 1.5f);
            using DrawingPen seam = new(DrawingColor.FromArgb(255, 255, 220, 245), 1.5f);
            graphics.FillRectangle(fill, -10, -6, 20, 12);
            graphics.DrawRectangle(outline, -10, -6, 20, 12);
            graphics.DrawLine(seam, 0, -5, 0, 5);
        }

        using MemoryStream cursorStream = new();
        using (BinaryWriter writer = new(cursorStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            const int bitmapDataSize = 40 + 32 * 32 * 4 + 32 * 4;
            writer.Write((ushort)0);
            writer.Write((ushort)2);
            writer.Write((ushort)1);
            writer.Write((byte)32);
            writer.Write((byte)32);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)16);
            writer.Write((ushort)16);
            writer.Write((uint)bitmapDataSize);
            writer.Write((uint)22);
            writer.Write(40);
            writer.Write(32);
            writer.Write(64);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(0);
            writer.Write(32 * 32 * 4);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            for (int y = 31; y >= 0; y--)
            {
                for (int x = 0; x < 32; x++)
                {
                    DrawingColor pixel = bitmap.GetPixel(x, y);
                    writer.Write(pixel.B);
                    writer.Write(pixel.G);
                    writer.Write(pixel.R);
                    writer.Write(pixel.A);
                }
            }

            writer.Write(new byte[32 * 4]);
        }

        cursorStream.Position = 0;
        return new Cursor(cursorStream);
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

    private sealed record Stroke(Polyline Polyline);
}
