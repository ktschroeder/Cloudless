using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Cloudless;

internal static class WindowCollisionManager
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private static Dictionary<MainWindow, Rect>? _selectionGroupInitialBounds;
    private static Vector _cachedGroupRequest;
    private static Vector _cachedGroupDelta;
    private static bool _hasCachedGroupDelta;

    internal static bool IsInGroupMove(MainWindow window) => _selectionGroupInitialBounds?.ContainsKey(window) == true;

    internal static void BeginGroupMove(IEnumerable<MainWindow> windows)
    {
        _selectionGroupInitialBounds = windows
            .Select(window => TryGetBounds(window, out Rect bounds) ? (Window: window, Bounds: bounds) : default)
            .Where(item => item.Window != null)
            .ToDictionary(item => item.Window, item => item.Bounds);
        _hasCachedGroupDelta = false;
    }

    internal static void EndGroupMove()
    {
        _selectionGroupInitialBounds = null;
        _hasCachedGroupDelta = false;
    }

    internal static Rect Constrain(MainWindow window, Rect current, Rect proposed, bool isResizing)
    {
        if (!isResizing && _selectionGroupInitialBounds is not null &&
            _selectionGroupInitialBounds.TryGetValue(window, out Rect initialBounds))
        {
            Vector requestedDelta = new(proposed.Left - initialBounds.Left, proposed.Top - initialBounds.Top);
            Vector constrainedDelta = ConstrainGroupDelta(requestedDelta);
            return new Rect(
                initialBounds.Left + constrainedDelta.X,
                initialBounds.Top + constrainedDelta.Y,
                proposed.Width,
                proposed.Height);
        }

        Rect[] peers = GlobalMouseModeManager.GetWindows()
            .Where(other => other != window && other.AvoidOverlap && other.IsVisible && other.WindowState != WindowState.Minimized)
            .Select(other => TryGetBounds(other, out Rect bounds) ? (Rect?)bounds : null)
            .Where(bounds => bounds.HasValue)
            .Select(bounds => bounds.GetValueOrDefault())
            .ToArray();

        return ConstrainBounds(current, proposed, GetVirtualDesktopBounds(), peers, isResizing);
    }

    internal static bool OverlapsWithArea(Rect first, Rect second) =>
        first.Left < second.Right && first.Right > second.Left &&
        first.Top < second.Bottom && first.Bottom > second.Top;

    private static Vector ConstrainGroupDelta(Vector requestedDelta)
    {
        if (_hasCachedGroupDelta && _cachedGroupRequest == requestedDelta)
            return _cachedGroupDelta;

        Dictionary<MainWindow, Rect> groupBounds = _selectionGroupInitialBounds!;
        HashSet<MainWindow> groupWindows = groupBounds.Keys.ToHashSet();
        Rect[] peers = GlobalMouseModeManager.GetWindows()
            .Where(window => !groupWindows.Contains(window) && window.AvoidOverlap && window.IsVisible && window.WindowState != WindowState.Minimized)
            .Select(window => TryGetBounds(window, out Rect bounds) ? (Rect?)bounds : null)
            .Where(bounds => bounds.HasValue)
            .Select(bounds => bounds.GetValueOrDefault())
            .ToArray();
        Rect display = GetVirtualDesktopBounds();
        Rect[] movingBounds = groupBounds
            .Where(item => item.Key.AvoidOverlap)
            .Select(item => item.Value)
            .ToArray();

        double deltaX = requestedDelta.X;
        double deltaY = requestedDelta.Y;
        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            deltaX = ConstrainGroupAxis(movingBounds, peers, display, deltaX, horizontal: true);
            movingBounds = movingBounds.Select(bounds => new Rect(bounds.Left + deltaX, bounds.Top, bounds.Width, bounds.Height)).ToArray();
            deltaY = ConstrainGroupAxis(movingBounds, peers, display, deltaY, horizontal: false);
        }
        else
        {
            deltaY = ConstrainGroupAxis(movingBounds, peers, display, deltaY, horizontal: false);
            movingBounds = movingBounds.Select(bounds => new Rect(bounds.Left, bounds.Top + deltaY, bounds.Width, bounds.Height)).ToArray();
            deltaX = ConstrainGroupAxis(movingBounds, peers, display, deltaX, horizontal: true);
        }

        _cachedGroupRequest = requestedDelta;
        _cachedGroupDelta = new Vector(deltaX, deltaY);
        _hasCachedGroupDelta = true;
        return _cachedGroupDelta;
    }

    private static double ConstrainGroupAxis(Rect[] movingBounds, Rect[] peers, Rect display, double requestedDelta, bool horizontal)
    {
        double constrainedDelta = requestedDelta;
        foreach (Rect current in movingBounds)
        {
            HashSet<Rect> initiallyOverlapping = peers.Where(peer => OverlapsWithArea(current, peer)).ToHashSet();
            Rect result = horizontal
                ? MoveHorizontally(current, constrainedDelta, display, peers, initiallyOverlapping)
                : MoveVertically(current, constrainedDelta, display, peers, initiallyOverlapping);
            double allowedDelta = horizontal ? result.Left - current.Left : result.Top - current.Top;
            constrainedDelta = requestedDelta > 0
                ? Math.Min(constrainedDelta, allowedDelta)
                : requestedDelta < 0 ? Math.Max(constrainedDelta, allowedDelta) : 0;
        }

        return constrainedDelta;
    }

    internal static Rect ConstrainBounds(Rect current, Rect proposed, Rect display, IReadOnlyList<Rect> peers, bool isResizing)
    {
        HashSet<Rect> initiallyOverlapping = peers.Where(peer => OverlapsWithArea(current, peer)).ToHashSet();
        return isResizing
            ? ConstrainResize(current, proposed, display, peers, initiallyOverlapping)
            : ConstrainMove(current, proposed, display, peers, initiallyOverlapping);
    }

    private static Rect ConstrainMove(
        Rect current,
        Rect proposed,
        Rect display,
        IReadOnlyList<Rect> peers,
        HashSet<Rect> initiallyOverlapping)
    {
        double deltaX = proposed.Left - current.Left;
        double deltaY = proposed.Top - current.Top;
        Rect result = current;

        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            result = MoveHorizontally(result, deltaX, display, peers, initiallyOverlapping);
            result = MoveVertically(result, deltaY, display, peers, initiallyOverlapping);
        }
        else
        {
            result = MoveVertically(result, deltaY, display, peers, initiallyOverlapping);
            result = MoveHorizontally(result, deltaX, display, peers, initiallyOverlapping);
        }

        return result;
    }

    private static Rect MoveHorizontally(
        Rect current,
        double delta,
        Rect display,
        IReadOnlyList<Rect> peers,
        HashSet<Rect> initiallyOverlapping)
    {
        double minDelta = display.Left - current.Left;
        double maxDelta = display.Right - current.Right;
        if (maxDelta < minDelta)
            maxDelta = minDelta;
        delta = Math.Clamp(delta, minDelta, maxDelta);

        foreach (Rect bounds in peers)
        {
            if (initiallyOverlapping.Contains(bounds) || current.Bottom <= bounds.Top || current.Top >= bounds.Bottom)
                continue;

            if (delta > 0 && current.Right <= bounds.Left && current.Right + delta > bounds.Left)
                delta = Math.Min(delta, bounds.Left - current.Right);
            else if (delta < 0 && current.Left >= bounds.Right && current.Left + delta < bounds.Right)
                delta = Math.Max(delta, bounds.Right - current.Left);
        }

        return new Rect(current.Left + delta, current.Top, current.Width, current.Height);
    }

    private static Rect MoveVertically(
        Rect current,
        double delta,
        Rect display,
        IReadOnlyList<Rect> peers,
        HashSet<Rect> initiallyOverlapping)
    {
        double minDelta = display.Top - current.Top;
        double maxDelta = display.Bottom - current.Bottom;
        if (maxDelta < minDelta)
            maxDelta = minDelta;
        delta = Math.Clamp(delta, minDelta, maxDelta);

        foreach (Rect bounds in peers)
        {
            if (initiallyOverlapping.Contains(bounds) || current.Right <= bounds.Left || current.Left >= bounds.Right)
                continue;

            if (delta > 0 && current.Bottom <= bounds.Top && current.Bottom + delta > bounds.Top)
                delta = Math.Min(delta, bounds.Top - current.Bottom);
            else if (delta < 0 && current.Top >= bounds.Bottom && current.Top + delta < bounds.Bottom)
                delta = Math.Max(delta, bounds.Bottom - current.Top);
        }

        return new Rect(current.Left, current.Top + delta, current.Width, current.Height);
    }

    private static Rect ConstrainResize(
        Rect current,
        Rect proposed,
        Rect display,
        IReadOnlyList<Rect> peers,
        HashSet<Rect> initiallyOverlapping)
    {
        double left = proposed.Left;
        double top = proposed.Top;
        double right = proposed.Right;
        double bottom = proposed.Bottom;
        bool leftChanged = Math.Abs(left - current.Left) > 0.5;
        bool rightChanged = Math.Abs(right - current.Right) > 0.5;
        bool topChanged = Math.Abs(top - current.Top) > 0.5;
        bool bottomChanged = Math.Abs(bottom - current.Bottom) > 0.5;

        if (leftChanged && !rightChanged)
            left = Math.Max(left, display.Left);
        else if (left < display.Left)
        {
            right += display.Left - left;
            left = display.Left;
        }

        if (rightChanged && !leftChanged)
            right = Math.Min(right, display.Right);
        else if (right > display.Right)
        {
            left -= right - display.Right;
            right = display.Right;
        }

        if (topChanged && !bottomChanged)
            top = Math.Max(top, display.Top);
        else if (top < display.Top)
        {
            bottom += display.Top - top;
            top = display.Top;
        }

        if (bottomChanged && !topChanged)
            bottom = Math.Min(bottom, display.Bottom);
        else if (bottom > display.Bottom)
        {
            top -= bottom - display.Bottom;
            bottom = display.Bottom;
        }

        foreach (Rect bounds in peers)
        {
            if (initiallyOverlapping.Contains(bounds))
                continue;

            if (top < bounds.Bottom && bottom > bounds.Top)
            {
                if (leftChanged && current.Left >= bounds.Right && left < bounds.Right)
                    left = bounds.Right;
                if (rightChanged && current.Right <= bounds.Left && right > bounds.Left)
                    right = bounds.Left;
            }

            if (left < bounds.Right && right > bounds.Left)
            {
                if (topChanged && current.Top >= bounds.Bottom && top < bounds.Bottom)
                    top = bounds.Bottom;
                if (bottomChanged && current.Bottom <= bounds.Top && bottom > bounds.Top)
                    bottom = bounds.Top;
            }
        }

        Rect result = new(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        return ClampToDisplay(result, display);
    }

    private static Rect ClampToDisplay(Rect rectangle, Rect display)
    {
        double width = Math.Min(rectangle.Width, display.Width);
        double height = Math.Min(rectangle.Height, display.Height);
        double left = Math.Clamp(rectangle.Left, display.Left, display.Right - width);
        double top = Math.Clamp(rectangle.Top, display.Top, display.Bottom - height);
        return new Rect(left, top, width, height);
    }

    private static Rect GetVirtualDesktopBounds()
    {
        double left = GetSystemMetrics(SmXVirtualScreen);
        double top = GetSystemMetrics(SmYVirtualScreen);
        double width = Math.Max(1, GetSystemMetrics(SmCxVirtualScreen));
        double height = Math.Max(1, GetSystemMetrics(SmCyVirtualScreen));
        return new Rect(left, top, width, height);
    }

    internal static bool TryGetBounds(MainWindow window, out Rect bounds)
    {
        if (GetWindowRect(new WindowInteropHelper(window).Handle, out NativeRect rectangle))
        {
            bounds = new Rect(
                rectangle.Left,
                rectangle.Top,
                rectangle.Right - rectangle.Left,
                rectangle.Bottom - rectangle.Top);
            return !bounds.IsEmpty;
        }

        bounds = Rect.Empty;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
