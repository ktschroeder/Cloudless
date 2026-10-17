using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Cloudless
{
    internal sealed class DebugInfoWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private readonly MainWindow _owner;
        private readonly TextBlock _debugTextBlock;
        private const int WindowMargin = 10;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        internal DebugInfoWindow(MainWindow owner)
        {
            _owner = owner;
            Owner = owner;
            SecondaryWindowOpacity.Apply(this);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = false;
            Focusable = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            SizeToContent = SizeToContent.WidthAndHeight;

            _debugTextBlock = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            _debugTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryForeground");

            Border border = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(WindowMargin),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(5),
                Child = _debugTextBlock,
                IsHitTestVisible = false
            };
            border.SetResourceReference(Border.BackgroundProperty, "OverlayBackground");
            Content = border;

            SourceInitialized += OnSourceInitialized;
            Closed += OnClosed;
            owner.LocationChanged += Owner_LocationOrSizeChanged;
            owner.SizeChanged += Owner_LocationOrSizeChanged;
            owner.StateChanged += Owner_StateChanged;
        }

        internal string DebugText
        {
            get => _debugTextBlock.Text;
            set
            {
                if (_debugTextBlock.Text == value)
                    return;

                _debugTextBlock.Text = value;
                if (IsVisible)
                    AlignToOwner();
            }
        }

        internal void AlignToOwner()
        {
            if (_owner.WindowState == WindowState.Minimized)
                return;

            _debugTextBlock.MaxWidth = Math.Max(100, _owner.ActualWidth - WindowMargin * 2);
            FrameworkElement content = (FrameworkElement)Content;
            content.Measure(new Size(_owner.ActualWidth, double.PositiveInfinity));
            UpdateLayout();
            Left = _owner.Left;
            double height = ActualHeight > 0 ? ActualHeight : content.DesiredSize.Height;
            Top = _owner.Top + _owner.ActualHeight - height;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        }

        private void Owner_LocationOrSizeChanged(object? sender, EventArgs e) => AlignToOwner();

        private void Owner_StateChanged(object? sender, EventArgs e)
        {
            if (_owner.WindowState != WindowState.Minimized)
                AlignToOwner();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _owner.LocationChanged -= Owner_LocationOrSizeChanged;
            _owner.SizeChanged -= Owner_LocationOrSizeChanged;
            _owner.StateChanged -= Owner_StateChanged;
        }
    }
}
