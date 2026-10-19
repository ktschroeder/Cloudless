using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Bitmap = System.Drawing.Bitmap;
using Graphics = System.Drawing.Graphics;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using ShapeEllipse = System.Windows.Shapes.Ellipse;
using ShapeLine = System.Windows.Shapes.Line;
using ShapePath = System.Windows.Shapes.Path;

namespace Cloudless
{
    internal sealed class MouseModeRadialWindow : Window
    {
        private const double RadialInnerRadius = 48;
        private const double RadialDeadZone = RadialInnerRadius;
        private const double RadialOuterRadius = 142;
        private readonly record struct PixelOffset(int X, int Y);
        private readonly record struct ModeContentOffsets(PixelOffset Icon, PixelOffset Label);
        private static readonly IReadOnlyDictionary<CloudlessMouseMode, ModeContentOffsets> ContentOffsets = new Dictionary<CloudlessMouseMode, ModeContentOffsets>
        {
            [CloudlessMouseMode.Normal] = new(new PixelOffset(10, 0), new PixelOffset(0, 0)),
            [CloudlessMouseMode.Control] = new(new PixelOffset(10, -2), new PixelOffset(-5, -5)),
            [CloudlessMouseMode.Selection] = new(new PixelOffset(0, 5), new PixelOffset(8, 10)),
            [CloudlessMouseMode.PageControl] = new(new PixelOffset(13, 8), new PixelOffset(-9 , 7)),
            [CloudlessMouseMode.Arrangement] = new(new PixelOffset(0, 0), new PixelOffset(0, 10)),
            [CloudlessMouseMode.Annotation] = new(new PixelOffset(0, 0), new PixelOffset(0, 0)),
            [CloudlessMouseMode.Spotlight] = new(new PixelOffset(5, 3), new PixelOffset(5, 3))
        };
        private readonly Point _anchorScreenPosition;
        private readonly CloudlessMouseMode _currentMode;
        private readonly Dictionary<CloudlessMouseMode, ShapePath> _choices = new();
        private bool _isDarkTheme;
        private Point? _lastScreenPosition;
        private ShapeLine? _cursorLine;
        private ShapeEllipse? _cursorMarker;
        private CloudlessMouseMode? _highlightedMode;

        internal MouseModeRadialWindow(MainWindow owner, Point anchorScreenPosition)
        {
            _anchorScreenPosition = anchorScreenPosition;
            _currentMode = GlobalMouseModeManager.CurrentMode;
            _isDarkTheme = string.Equals(Properties.Settings.Default["Theme"] as string, "Dark", System.StringComparison.OrdinalIgnoreCase);
            Owner = owner;
            SecondaryWindowOpacity.Apply(this);
            const double cursorMargin = 12;
            Left = SystemParameters.VirtualScreenLeft - cursorMargin;
            Top = SystemParameters.VirtualScreenTop - cursorMargin;
            Width = SystemParameters.VirtualScreenWidth + cursorMargin * 2;
            Height = SystemParameters.VirtualScreenHeight + cursorMargin * 2;
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
            Content = new Canvas { Width = Width, Height = Height };
            Loaded += (_, _) =>
            {
                InitializeSelector();
                UpdateHighlight(_anchorScreenPosition);
            };
        }

        internal CloudlessMouseMode? GetModeAtScreenPosition(Point screenPosition)
        {
            return GetModeAtScreenPosition(_anchorScreenPosition, screenPosition);
        }

        internal static CloudlessMouseMode? GetModeAtScreenPosition(Point anchorScreenPosition, Point screenPosition)
        {
            Vector offset = screenPosition - anchorScreenPosition;
            if (offset.Length < RadialDeadZone)
                return null;

            double angle = Math.Atan2(offset.Y, offset.X) * 180 / Math.PI;
            int sector = ((int)Math.Floor((angle + 22.5 + 360) / 45)) % 8;
            return sector switch
            {
                0 => CloudlessMouseMode.Spotlight,
                4 => null,
                1 => CloudlessMouseMode.Selection,
                2 => CloudlessMouseMode.Arrangement,
                3 => CloudlessMouseMode.PageControl,
                5 => CloudlessMouseMode.Annotation,
                6 => CloudlessMouseMode.Normal,
                7 => CloudlessMouseMode.Control,
                _ => null
            };
        }

        internal void UpdateHighlight(Point screenPosition)
        {
            InitializeSelector();
            _lastScreenPosition = screenPosition;
            if (_cursorLine == null || _cursorMarker == null)
                return;

            Point cursorPosition = PointFromScreen(screenPosition);
            if (_cursorLine != null)
            {
                _cursorLine.X2 = cursorPosition.X;
                _cursorLine.Y2 = cursorPosition.Y;
            }

            if (_cursorMarker != null)
            {
                Canvas.SetLeft(_cursorMarker, cursorPosition.X - _cursorMarker.Width / 2);
                Canvas.SetTop(_cursorMarker, cursorPosition.Y - _cursorMarker.Height / 2);
            }

            CloudlessMouseMode? mode = GetModeAtScreenPosition(screenPosition);
            if (_highlightedMode == mode)
                return;

            _highlightedMode = mode;
            foreach ((CloudlessMouseMode choice, ShapePath path) in _choices)
            {
                bool isCurrentMode = choice == _currentMode;
                bool isHighlighted = choice == mode && !isCurrentMode;
                path.Fill = new SolidColorBrush(GetSectorFill(isCurrentMode, isHighlighted));
                path.Stroke = new SolidColorBrush(GetSectorStroke(isCurrentMode, isHighlighted));
            }
        }

        internal void UpdateTheme(string? themeName)
        {
            bool isDarkTheme = string.Equals(themeName, "Dark", System.StringComparison.OrdinalIgnoreCase);
            if (_isDarkTheme == isDarkTheme)
                return;

            _isDarkTheme = isDarkTheme;
            if (_cursorLine == null)
                return;

            _choices.Clear();
            _cursorLine = null;
            _cursorMarker = null;
            _highlightedMode = null;
            InitializeSelector();
            UpdateHighlight(_lastScreenPosition ?? _anchorScreenPosition);
        }

        private void InitializeSelector()
        {
            if (_cursorLine != null)
                return;

            Point centerPosition = PointFromScreen(_anchorScreenPosition);
            Content = CreateContent(centerPosition);
        }

        private Canvas CreateContent(Point centerPosition)
        {
            ShapePath[] sectorPaths = new ShapePath[8];
            Canvas canvas = new()
            {
                Width = Width,
                Height = Height
            };

            for (int sector = 0; sector < 8; sector++)
            {
                double centerAngle = sector * 45;
                ShapePath path = CreateSectorPath(centerPosition.X, centerPosition.Y, centerAngle - 22.5 + 1.5, centerAngle + 22.5 - 1.5);
                bool isUnusedSector = sector == 4;
                path.Fill = new SolidColorBrush(_isDarkTheme
                    ? isUnusedSector ? Color.FromArgb(90, 35, 35, 35) : Color.FromArgb(220, 35, 35, 35)
                    : isUnusedSector ? Color.FromArgb(100, 235, 235, 235) : Color.FromArgb(225, 250, 250, 250));
                path.Stroke = new SolidColorBrush(_isDarkTheme
                    ? isUnusedSector ? Color.FromArgb(100, 145, 145, 145) : Color.FromArgb(230, 145, 145, 145)
                    : isUnusedSector ? Color.FromArgb(100, 80, 80, 80) : Color.FromArgb(210, 90, 90, 90));
                path.StrokeThickness = 1.5;
                canvas.Children.Add(path);
                sectorPaths[sector] = path;
            }

            _cursorLine = new ShapeLine
            {
                X1 = centerPosition.X,
                Y1 = centerPosition.Y,
                X2 = centerPosition.X,
                Y2 = centerPosition.Y,
                Stroke = new SolidColorBrush(_isDarkTheme ? Color.FromArgb(235, 235, 245, 255) : Color.FromArgb(235, 40, 75, 115)),
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            canvas.Children.Add(_cursorLine);

            AddChoice(canvas, centerPosition, sectorPaths[6], CloudlessMouseMode.Normal, "➤", "Standard", 6);
            AddChoice(canvas, centerPosition, sectorPaths[7], CloudlessMouseMode.Control, "✥", "Exploration\nControl", 7);
            AddChoice(canvas, centerPosition, sectorPaths[1], CloudlessMouseMode.Selection, "⌖", "Selection", 1);
            AddChoice(canvas, centerPosition, sectorPaths[3], CloudlessMouseMode.PageControl, "↕", "Page Control", 3);
            AddChoice(canvas, centerPosition, sectorPaths[2], CloudlessMouseMode.Arrangement, "✣", "Arrangement", 2);
            AddChoice(canvas, centerPosition, sectorPaths[5], CloudlessMouseMode.Annotation, "✎", "Annotation", 5);
            AddChoice(canvas, centerPosition, sectorPaths[0], CloudlessMouseMode.Spotlight, "◉", "Spotlight", 0);

            ShapePath currentModePath = _choices[_currentMode];
            currentModePath.Fill = new SolidColorBrush(GetSectorFill(isCurrentMode: true, isHighlighted: false));
            currentModePath.Stroke = new SolidColorBrush(GetSectorStroke(isCurrentMode: true, isHighlighted: false));

            Border center = new()
            {
                Width = 76,
                Height = 76,
                CornerRadius = new CornerRadius(38),
                Background = new SolidColorBrush(_isDarkTheme ? Color.FromArgb(230, 25, 25, 25) : Color.FromArgb(240, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(_isDarkTheme ? Color.FromArgb(230, 145, 145, 145) : Color.FromArgb(230, 90, 90, 90)),
                BorderThickness = new Thickness(1.5),
                Child = new TextBlock
                {
                    Text = "MOUSE\nMODE",
                    Foreground = _isDarkTheme ? Brushes.White : Brushes.Black,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(center, centerPosition.X - center.Width / 2);
            Canvas.SetTop(center, centerPosition.Y - center.Height / 2);
            canvas.Children.Add(center);

            _cursorMarker = new ShapeEllipse
            {
                Width = 9,
                Height = 9,
                Fill = new SolidColorBrush(_isDarkTheme ? Color.FromArgb(245, 235, 245, 255) : Color.FromArgb(245, 40, 75, 115)),
                Stroke = new SolidColorBrush(Color.FromArgb(255, 30, 35, 45)),
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_cursorMarker, centerPosition.X - _cursorMarker.Width / 2);
            Canvas.SetTop(_cursorMarker, centerPosition.Y - _cursorMarker.Height / 2);
            canvas.Children.Add(_cursorMarker);
            return canvas;
        }

        private static ShapePath CreateSectorPath(double centerX, double centerY, double startAngle, double endAngle)
        {
            Point outerStart = GetPoint(centerX, centerY, RadialOuterRadius, startAngle);
            Point outerEnd = GetPoint(centerX, centerY, RadialOuterRadius, endAngle);
            Point innerEnd = GetPoint(centerX, centerY, RadialInnerRadius, endAngle);
            Point innerStart = GetPoint(centerX, centerY, RadialInnerRadius, startAngle);

            PathFigure figure = new() { StartPoint = outerStart, IsClosed = true };
            figure.Segments.Add(new ArcSegment(outerEnd, new Size(RadialOuterRadius, RadialOuterRadius), 0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(innerEnd, true));
            figure.Segments.Add(new ArcSegment(innerStart, new Size(RadialInnerRadius, RadialInnerRadius), 0, false, SweepDirection.Counterclockwise, true));
            PathGeometry geometry = new();
            geometry.Figures.Add(figure);
            return new ShapePath { Data = geometry };
        }

        private static Point GetPoint(double centerX, double centerY, double radius, double angle)
        {
            double radians = angle * Math.PI / 180;
            return new Point(centerX + Math.Cos(radians) * radius, centerY + Math.Sin(radians) * radius);
        }

        private Color GetSectorFill(bool isCurrentMode, bool isHighlighted)
        {
            if (isHighlighted)
                return Color.FromArgb(245, 50, 115, 190);
            if (_isDarkTheme)
                return isCurrentMode ? Color.FromArgb(90, 35, 35, 35) : Color.FromArgb(220, 35, 35, 35);
            return isCurrentMode ? Color.FromArgb(155, 215, 225, 240) : Color.FromArgb(225, 250, 250, 250);
        }

        private Color GetSectorStroke(bool isCurrentMode, bool isHighlighted)
        {
            if (isHighlighted)
                return _isDarkTheme ? Colors.White : Color.FromRgb(35, 75, 125);
            if (_isDarkTheme)
                return isCurrentMode ? Color.FromArgb(100, 145, 145, 145) : Color.FromArgb(230, 145, 145, 145);
            return isCurrentMode ? Color.FromArgb(180, 75, 95, 125) : Color.FromArgb(210, 90, 90, 90);
        }

        private void AddChoice(Canvas canvas, Point centerPosition, ShapePath path, CloudlessMouseMode mode, string glyph, string label, int sector)
        {
            double centerAngle = sector * 45;
            _choices.Add(mode, path);

            double labelRadius = (RadialInnerRadius + RadialOuterRadius) / 2;
            Point labelPosition = GetPoint(centerPosition.X, centerPosition.Y, labelRadius, centerAngle);
            ModeContentOffsets offsets = ContentOffsets[mode];
            FrameworkElement icon = CreateChoiceIcon(mode, glyph);
            Canvas.SetLeft(icon, labelPosition.X - icon.Width / 2 + offsets.Icon.X);
            Canvas.SetTop(icon, labelPosition.Y - 40 + offsets.Icon.Y);
            TextBlock labelBlock = new()
            {
                Text = label,
                Width = 100,
                Height = 34,
                Foreground = _isDarkTheme ? Brushes.White : Brushes.Black,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(labelBlock, labelPosition.X - labelBlock.Width / 2 + offsets.Label.X);
            Canvas.SetTop(labelBlock, labelPosition.Y + 2 + offsets.Label.Y);
            if (mode == _currentMode)
            {
                icon.Opacity = 0.45;
                labelBlock.Opacity = 0.45;
            }

            canvas.Children.Add(icon);
            canvas.Children.Add(labelBlock);
        }

        private FrameworkElement CreateChoiceIcon(CloudlessMouseMode mode, string glyph)
        {
            ImageSource? cursorImage = CreateCursorImage(mode);
            if (cursorImage != null)
            {
                Grid icon = new() { Width = 44, Height = 44 };
                icon.Children.Add(new Image
                {
                    Source = cursorImage,
                    Width = 32,
                    Height = 32,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                return icon;
            }

            return new TextBlock
            {
                Text = glyph,
                Width = 44,
                Height = 44,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 32,
                Foreground = _isDarkTheme ? Brushes.White : Brushes.Black,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        internal static ImageSource? CreateCursorImage(CloudlessMouseMode mode)
        {
            IntPtr cursor = LoadModeCursor(mode, out bool shouldDestroy);
            if (cursor == IntPtr.Zero)
                return null;

            try
            {
                using Bitmap bitmap = new(32, 32, PixelFormat.Format32bppArgb);
                using Graphics graphics = Graphics.FromImage(bitmap);
                IntPtr deviceContext = graphics.GetHdc();
                try
                {
                    if (!DrawIconEx(deviceContext, 0, 0, cursor, 32, 32, 0, IntPtr.Zero, DI_NORMAL))
                        return null;
                }
                finally
                {
                    graphics.ReleaseHdc(deviceContext);
                }

                IntPtr bitmapHandle = bitmap.GetHbitmap();
                try
                {
                    BitmapSource image = Imaging.CreateBitmapSourceFromHBitmap(
                        bitmapHandle,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze();
                    return image;
                }
                finally
                {
                    DeleteObject(bitmapHandle);
                }
            }
            catch (ExternalException)
            {
                return null;
            }
            finally
            {
                if (shouldDestroy)
                    DestroyCursor(cursor);
            }
        }

        private static IntPtr LoadModeCursor(CloudlessMouseMode mode, out bool shouldDestroy)
        {
            shouldDestroy = false;
            if (mode == CloudlessMouseMode.Spotlight)
                return IntPtr.Zero;
            if (mode == CloudlessMouseMode.Normal)
                return LoadCursor(IntPtr.Zero, new IntPtr(IDC_ARROW));
            if (mode == CloudlessMouseMode.Control)
                return LoadCursor(IntPtr.Zero, new IntPtr(IDC_SIZEALL));
            if (mode == CloudlessMouseMode.Arrangement)
            {
                IntPtr arrangementCursor = LoadCursorResource("custom-cursor-arrangement.cur");
                shouldDestroy = arrangementCursor != IntPtr.Zero;
                return arrangementCursor;
            }
            if (mode == CloudlessMouseMode.Annotation)
                return LoadCursor(IntPtr.Zero, new IntPtr(IDC_PEN));

            string cursorFile = mode == CloudlessMouseMode.Selection ? "custom_cursor.cur" : "page-cursor.cur";
            IntPtr cursor = LoadCursorResource(cursorFile);
            shouldDestroy = cursor != IntPtr.Zero;
            return cursor;
        }

        private static IntPtr LoadCursorResource(string cursorFile)
        {
            var resource = Application.GetResourceStream(
                new Uri($"pack://application:,,,/Cloudless;component/{cursorFile}", UriKind.Absolute));
            if (resource?.Stream == null)
                return IntPtr.Zero;

            using Stream stream = resource.Stream;
            using MemoryStream memory = new();
            stream.CopyTo(memory);
            byte[] cursorFileData = memory.ToArray();
            if (cursorFileData.Length < 6)
                return IntPtr.Zero;

            int entryCount = BitConverter.ToUInt16(cursorFileData, 4);
            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                int entryOffset = 6 + entryIndex * 16;
                if (entryOffset + 16 > cursorFileData.Length)
                    break;

                int imageSize = checked((int)BitConverter.ToUInt32(cursorFileData, entryOffset + 8));
                int imageOffset = checked((int)BitConverter.ToUInt32(cursorFileData, entryOffset + 12));
                if (imageSize <= 0 || imageOffset < 0 || imageOffset + imageSize > cursorFileData.Length)
                    continue;

                byte[] imageData = new byte[imageSize + 4];
                Buffer.BlockCopy(cursorFileData, entryOffset + 10, imageData, 0, 4);
                Buffer.BlockCopy(cursorFileData, imageOffset, imageData, 4, imageSize);
                IntPtr cursor = CreateIconFromResourceEx(imageData, (uint)imageData.Length, false, 0x00030000, 0, 0, 0x00000040);
                if (cursor != IntPtr.Zero)
                    return cursor;
            }

            return IntPtr.Zero;
        }

        private const int IDC_ARROW = 32512;
        private const int IDC_SIZEALL = 32646;
        private const int IDC_PEN = 32631;
        private const uint DI_NORMAL = 0x0003;

        [DllImport("user32.dll", EntryPoint = "LoadCursorW", SetLastError = true)]
        private static extern IntPtr LoadCursor(IntPtr instance, IntPtr cursorName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateIconFromResourceEx(byte[] resource, uint size, [MarshalAs(UnmanagedType.Bool)] bool isIcon, uint version, int width, int height, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DrawIconEx(IntPtr deviceContext, int x, int y, IntPtr icon, int width, int height, uint step, IntPtr flickerFreeDraw, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyCursor(IntPtr cursor);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr graphicsObject);

    }
}
