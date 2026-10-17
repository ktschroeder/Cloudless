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

namespace Cloudless
{
    internal sealed class MouseModeRadialWindow : Window
    {
        private const double RadialDeadZone = 32;
        private readonly Point _anchorScreenPosition;
        private readonly Dictionary<CloudlessMouseMode, Border> _choices = new();
        private CloudlessMouseMode? _highlightedMode;

        internal MouseModeRadialWindow(MainWindow owner, Point anchorScreenPosition)
        {
            _anchorScreenPosition = anchorScreenPosition;
            Owner = owner;
            SecondaryWindowOpacity.Apply(this);
            Width = 300;
            Height = 300;
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
            Left = anchorScreenPosition.X - Width / 2;
            Top = anchorScreenPosition.Y - Height / 2;
            Content = CreateContent();
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

            if (Math.Abs(offset.X) > Math.Abs(offset.Y))
                return offset.X > 0 ? CloudlessMouseMode.Control : CloudlessMouseMode.PageControl;

            return offset.Y > 0 ? CloudlessMouseMode.Selection : CloudlessMouseMode.Normal;
        }

        internal void UpdateHighlight(Point screenPosition)
        {
            CloudlessMouseMode? mode = GetModeAtScreenPosition(screenPosition);
            if (_highlightedMode == mode)
                return;

            _highlightedMode = mode;
            foreach ((CloudlessMouseMode choice, Border border) in _choices)
            {
                bool isHighlighted = choice == mode;
                border.Background = new SolidColorBrush(isHighlighted
                    ? Color.FromArgb(240, 50, 115, 190)
                    : Color.FromArgb(220, 35, 35, 35));
                border.BorderBrush = new SolidColorBrush(isHighlighted
                    ? Colors.White
                    : Color.FromArgb(230, 145, 145, 145));
            }
        }

        private Grid CreateContent()
        {
            Grid grid = new() { Margin = new Thickness(8) };
            for (int i = 0; i < 3; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            AddChoice(grid, CloudlessMouseMode.Normal, "➤", "Standard", 0, 1);
            AddChoice(grid, CloudlessMouseMode.Control, "✥", "Exploration\nControl", 1, 2);
            AddChoice(grid, CloudlessMouseMode.Selection, "⌖", "Selection", 2, 1);
            AddChoice(grid, CloudlessMouseMode.PageControl, "↕", "Page Control", 1, 0);

            Border center = new()
            {
                Width = 76,
                Height = 76,
                CornerRadius = new CornerRadius(38),
                Background = new SolidColorBrush(Color.FromArgb(230, 25, 25, 25)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(230, 145, 145, 145)),
                BorderThickness = new Thickness(1.5),
                Child = new TextBlock
                {
                    Text = "MOUSE\nMODE",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetRow(center, 1);
            Grid.SetColumn(center, 1);
            grid.Children.Add(center);
            return grid;
        }

        private void AddChoice(Grid grid, CloudlessMouseMode mode, string glyph, string label, int row, int column)
        {
            Border border = new()
            {
                Margin = new Thickness(5),
                Padding = new Thickness(5),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1.5),
                Background = new SolidColorBrush(Color.FromArgb(220, 35, 35, 35)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(230, 145, 145, 145)),
                Child = CreateChoiceContent(mode, glyph, label)
            };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, column);
            grid.Children.Add(border);
            _choices.Add(mode, border);
        }

        private static StackPanel CreateChoiceContent(CloudlessMouseMode mode, string glyph, string label)
        {
            StackPanel stack = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            ImageSource? cursorImage = CreateCursorImage(mode);
            if (cursorImage != null)
            {
                stack.Children.Add(new Image
                {
                    Source = cursorImage,
                    Width = 32,
                    Height = 32,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
            else
            {
                stack.Children.Add(new TextBlock
                {
                    Text = glyph,
                    FontFamily = new FontFamily("Segoe UI Symbol"),
                    FontSize = 32,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            stack.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = Brushes.White,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            return stack;
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
            if (mode == CloudlessMouseMode.Normal)
                return LoadCursor(IntPtr.Zero, new IntPtr(IDC_ARROW));
            if (mode == CloudlessMouseMode.Control)
                return LoadCursor(IntPtr.Zero, new IntPtr(IDC_SIZEALL));

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
