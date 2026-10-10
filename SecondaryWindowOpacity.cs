using System.Windows;

namespace Cloudless;

internal static class SecondaryWindowOpacity
{
    private const double TransparentOpacity = 0.9;

    internal static void Apply(Window window)
    {
        if (window is MainWindow)
            return;

        window.Opacity = Cloudless.Properties.Settings.Default.SlightlyTransparentSecondaryWindows
            ? TransparentOpacity
            : 1.0;
    }

    internal static void ApplyToOpenWindows()
    {
        if (Application.Current == null)
            return;

        foreach (Window window in Application.Current.Windows)
            Apply(window);
    }

}
