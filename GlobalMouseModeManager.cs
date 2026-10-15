using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;

namespace Cloudless;

internal enum CloudlessMouseMode
{
    Normal,
    Control,
    Selection
}

internal static class GlobalMouseModeManager
{
    private static readonly HashSet<MainWindow> Windows = new();
    private static readonly HashSet<MainWindow> SelectedWindows = new();
    private static MarqueeSelectionOverlayWindow? _marqueeOverlay;
    private static bool _suppressNextContextMenu;
    private static Point? _lastOverlapCyclePoint;
    private static MainWindow[] _lastOverlapCycleCandidates = Array.Empty<MainWindow>();
    private static MainWindow? _lastOverlapCycleWindow;
    private static readonly IntPtr HwndTop = new(0);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    internal static CloudlessMouseMode CurrentMode { get; private set; }
    internal static int SelectedWindowCount => SelectedWindows.Count;
    internal static bool ShouldSuppressNextContextMenu => _suppressNextContextMenu;
    internal static bool IsSelected(MainWindow window) => SelectedWindows.Contains(window);
    internal static MainWindow[] GetSelectedWindows() => SelectedWindows.ToArray();
    internal static MainWindow[] GetWindows() => Windows.ToArray();

    internal static void SetMarqueeSelectionRectangle(MainWindow owner, Rect? rectangle)
    {
        if (rectangle is Rect selectionRectangle)
        {
            if (_marqueeOverlay == null)
            {
                _marqueeOverlay = new MarqueeSelectionOverlayWindow(owner);
                MarqueeSelectionOverlayWindow overlay = _marqueeOverlay;
                overlay.Closed += (_, _) =>
                {
                    if (ReferenceEquals(_marqueeOverlay, overlay))
                        _marqueeOverlay = null;
                };
            }

            _marqueeOverlay.SetRectangle(selectionRectangle);
        }
        else
        {
            MarqueeSelectionOverlayWindow? overlay = _marqueeOverlay;
            _marqueeOverlay = null;
            overlay?.Close();
        }
    }

    internal static void Register(MainWindow window)
    {
        Windows.Add(window);
        window.ApplyGlobalMouseModeCursor(CurrentMode);
        window.SetWindowSelected(SelectedWindows.Contains(window));
    }

    internal static void Unregister(MainWindow window)
    {
        Windows.Remove(window);
        SelectedWindows.Remove(window);
        window.SetWindowSelected(false);
    }

    internal static void SetMode(CloudlessMouseMode mode)
    {
        ResetOverlapWindowCycle();
        CurrentMode = mode;
        foreach (MainWindow window in Windows)
            window.ApplyGlobalMouseModeCursor(mode);
    }

    internal static bool CycleOverlappingWindows(MainWindow source, Point screenPosition, int wheelDelta)
    {
        MainWindow[] candidates = GetWindowsAtScreenPosition(screenPosition);
        if (candidates.Length == 0)
        {
            ResetOverlapWindowCycle();
            return false;
        }

        if (candidates.Length == 1)
        {
            ResetOverlapWindowCycle();
            return true;
        }

        bool sameCandidateSet = _lastOverlapCycleCandidates.Length == candidates.Length &&
            _lastOverlapCycleCandidates.ToHashSet().SetEquals(candidates);
        bool continuingCycle = _lastOverlapCyclePoint == screenPosition && sameCandidateSet &&
            _lastOverlapCycleWindow != null;
        MainWindow[] cycleCandidates = continuingCycle ? _lastOverlapCycleCandidates : candidates;
        int currentIndex = continuingCycle
            ? Array.IndexOf(cycleCandidates, _lastOverlapCycleWindow)
            : Array.IndexOf(cycleCandidates, source);
        if (currentIndex < 0)
            currentIndex = 0;

        int direction = wheelDelta > 0 ? -1 : 1;
        int nextIndex = (currentIndex + direction + cycleCandidates.Length) % cycleCandidates.Length;
        MainWindow nextWindow = cycleCandidates[nextIndex];
        IntPtr nextHandle = new WindowInteropHelper(nextWindow).Handle;
        if (SetWindowPos(nextHandle, HwndTop, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate))
            nextWindow.EnsureAuxiliaryWindowsAboveOwner();

        _lastOverlapCyclePoint = screenPosition;
        _lastOverlapCycleCandidates = cycleCandidates;
        _lastOverlapCycleWindow = nextWindow;
        return true;
    }

    internal static void ResetOverlapWindowCycle()
    {
        _lastOverlapCyclePoint = null;
        _lastOverlapCycleCandidates = Array.Empty<MainWindow>();
        _lastOverlapCycleWindow = null;
    }

    private static MainWindow[] GetWindowsAtScreenPosition(Point screenPosition)
    {
        HashSet<MainWindow> remaining = Windows
            .Where(window => window.IsVisible && window.WindowState != WindowState.Minimized &&
                WindowCollisionManager.TryGetBounds(window, out Rect bounds) && bounds.Contains(screenPosition))
            .ToHashSet();
        if (remaining.Count < 2)
            return remaining.ToArray();

        var windowsByHandle = remaining.ToDictionary(window => new WindowInteropHelper(window).Handle);
        var ordered = new List<MainWindow>(remaining.Count);
        IntPtr handle = NativeMethods.GetTopWindow(IntPtr.Zero);
        while (handle != IntPtr.Zero && remaining.Count > 0)
        {
            if (windowsByHandle.TryGetValue(handle, out MainWindow? window) && remaining.Remove(window))
                ordered.Add(window);
            handle = NativeMethods.GetWindow(handle, NativeMethods.GW_HWNDNEXT);
        }

        ordered.AddRange(remaining);
        return ordered.ToArray();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    internal static void ToggleSelection(MainWindow window)
    {
        bool isSelected = SelectedWindows.Add(window);
        if (!isSelected)
            SelectedWindows.Remove(window);
        window.SetWindowSelected(isSelected);
    }

    internal static void SelectWindow(MainWindow window)
    {
        SelectedWindows.Add(window);
        window.SetWindowSelected(true);
    }

    internal static void DeselectWindow(MainWindow window)
    {
        SelectedWindows.Remove(window);
        window.SetWindowSelected(false);
    }

    internal static void InvertSelection()
    {
        foreach (MainWindow window in Windows)
        {
            bool isSelected = !SelectedWindows.Contains(window);
            if (isSelected)
                SelectedWindows.Add(window);
            else
                SelectedWindows.Remove(window);
            window.SetWindowSelected(isSelected);
        }
    }

    internal static void ClearSelection()
    {
        foreach (MainWindow window in SelectedWindows.ToArray())
        {
            SelectedWindows.Remove(window);
            window.SetWindowSelected(false);
        }
    }

    internal static void SuppressNextContextMenu() => _suppressNextContextMenu = true;

    internal static bool ConsumeContextMenuSuppression()
    {
        bool shouldSuppress = _suppressNextContextMenu;
        _suppressNextContextMenu = false;
        return shouldSuppress;
    }
}
