using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Cloudless;

internal static class SecondaryWindowShadowHitTesting
{
    private static readonly ConditionalWeakTable<Window, ShadowRegistration> Registrations = new();

    internal static void Configure(Window window)
    {
        Registrations.GetValue(window, static owner => new ShadowRegistration(owner));
    }

    private sealed class ShadowRegistration
    {
        private const int WmNcHitTest = 0x0084;
        private const int HtTransparent = -1;
        private readonly Window _window;
        private HwndSource? _source;
        private FrameworkElement? _shadowElement;

        internal ShadowRegistration(Window window)
        {
            _window = window;
            _window.Loaded += Window_Loaded;
            _window.Closed += Window_Closed;
            if (_window.IsLoaded)
                AttachHook();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => AttachHook();

        private void AttachHook()
        {
            if (_source != null)
                return;

            _shadowElement = FindShadowElement(_window.Content as DependencyObject);
            if (_shadowElement == null)
                return;

            _source = PresentationSource.FromVisual(_window) as HwndSource;
            _source?.AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != WmNcHitTest || _shadowElement == null ||
                PresentationSource.FromVisual(_shadowElement) == null ||
                _shadowElement.ActualWidth <= 0 || _shadowElement.ActualHeight <= 0)
                return IntPtr.Zero;

            long packedPoint = lParam.ToInt64();
            int screenX = unchecked((short)(packedPoint & 0xFFFF));
            int screenY = unchecked((short)((packedPoint >> 16) & 0xFFFF));
            Point pointInShadowElement = _shadowElement.PointFromScreen(new Point(screenX, screenY));
            Rect shadowElementBounds = new(0, 0, _shadowElement.ActualWidth, _shadowElement.ActualHeight);
            if (shadowElementBounds.Contains(pointInShadowElement))
                return IntPtr.Zero;

            handled = true;
            return new IntPtr(HtTransparent);
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            _window.Loaded -= Window_Loaded;
            _window.Closed -= Window_Closed;
            _source?.RemoveHook(WindowProc);
            _source = null;
        }

        private static FrameworkElement? FindShadowElement(DependencyObject? root)
        {
            if (root == null)
                return null;

            if (root is FrameworkElement element && element.Effect is DropShadowEffect)
                return element;

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int index = 0; index < childCount; index++)
            {
                FrameworkElement? child = FindShadowElement(VisualTreeHelper.GetChild(root, index));
                if (child != null)
                    return child;
            }

            return null;
        }
    }
}
