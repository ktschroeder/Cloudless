using System.Collections.Generic;

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
    private static bool _suppressNextContextMenu;

    internal static CloudlessMouseMode CurrentMode { get; private set; }
    internal static int SelectedWindowCount => SelectedWindows.Count;
    internal static bool ShouldSuppressNextContextMenu => _suppressNextContextMenu;
    internal static bool IsSelected(MainWindow window) => SelectedWindows.Contains(window);
    internal static MainWindow[] GetSelectedWindows() => SelectedWindows.ToArray();

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
        CurrentMode = mode;
        foreach (MainWindow window in Windows)
            window.ApplyGlobalMouseModeCursor(mode);
    }

    internal static void ToggleSelection(MainWindow window)
    {
        bool isSelected = SelectedWindows.Add(window);
        if (!isSelected)
            SelectedWindows.Remove(window);
        window.SetWindowSelected(isSelected);
    }

    internal static void SuppressNextContextMenu() => _suppressNextContextMenu = true;

    internal static bool ConsumeContextMenuSuppression()
    {
        bool shouldSuppress = _suppressNextContextMenu;
        _suppressNextContextMenu = false;
        return shouldSuppress;
    }
}
