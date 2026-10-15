using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class CropModeTests
{
    [Fact]
    public void CropModePreservesPanAndZoomBeforeAndAfterEntry()
    {
        RunOnStaThread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ThemeManager.ApplyThemeGlobalResources("Dark");
            bool originalDisableSmartZoom = Cloudless.Properties.Settings.Default.DisableSmartZoom;
            MainWindow? window = null;
            MainWindow? secondWindow = null;

            try
            {
                Cloudless.Properties.Settings.Default.DisableSmartZoom = true;
                window = CreateExplorationWindow();
                window.imageScaleTransform!.ScaleX = 1.75;
                window.imageScaleTransform.ScaleY = 1.75;
                window.imageTranslateTransform!.X = 37;
                window.imageTranslateTransform.Y = -24;

                InvokeZoom(window, new Point(window.ActualWidth / 2 + 80, window.ActualHeight / 2 + 45), 1.2);
                InvokePan(window, new Vector(12, -9));
                double expectedScaleX = window.imageScaleTransform.ScaleX;
                double expectedScaleY = window.imageScaleTransform.ScaleY;
                double expectedPanX = window.imageTranslateTransform.X;
                double expectedPanY = window.imageTranslateTransform.Y;

                InvokeCropMode(window, true);

                Assert.Equal(expectedScaleX, window.imageScaleTransform.ScaleX);
                Assert.Equal(expectedScaleY, window.imageScaleTransform.ScaleY);
                Assert.Equal(expectedPanX, window.imageTranslateTransform.X);
                Assert.Equal(expectedPanY, window.imageTranslateTransform.Y);

                window.Close();
                window = null;

                secondWindow = CreateExplorationWindow();
                secondWindow.imageScaleTransform!.ScaleX = 1.5;
                secondWindow.imageScaleTransform.ScaleY = 1.5;
                InvokeCropMode(secondWindow, true);

                InvokeZoom(secondWindow, new Point(secondWindow.ActualWidth / 2 + 70, secondWindow.ActualHeight / 2 - 35), 1.3);
                InvokePan(secondWindow, new Vector(-18, 11));

                expectedScaleX = secondWindow.imageScaleTransform.ScaleX;
                expectedScaleY = secondWindow.imageScaleTransform.ScaleY;
                expectedPanX = secondWindow.imageTranslateTransform!.X;
                expectedPanY = secondWindow.imageTranslateTransform.Y;
                Assert.NotEqual(1.5, expectedScaleX);
                Assert.NotEqual(0, expectedPanX);
                Assert.NotEqual(0, expectedPanY);

                InvokeCropMode(secondWindow, false);

                Assert.Equal(expectedScaleX, secondWindow.imageScaleTransform.ScaleX);
                Assert.Equal(expectedScaleY, secondWindow.imageScaleTransform.ScaleY);
                Assert.Equal(expectedPanX, secondWindow.imageTranslateTransform.X);
                Assert.Equal(expectedPanY, secondWindow.imageTranslateTransform.Y);
            }
            finally
            {
                window?.Close();
                secondWindow?.Close();
                Cloudless.Properties.Settings.Default.DisableSmartZoom = originalDisableSmartZoom;
                ThemeManager.ApplyThemeGlobalResources(string.Empty);
                app.Shutdown();
            }
        });
    }

    private static MainWindow CreateExplorationWindow()
    {
        var window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
        window.Show();
        window.UpdateLayout();
        typeof(MainWindow).GetField("isExplorationMode", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(window, true);
        return window;
    }

    private static void InvokeCropMode(MainWindow window, bool enabled)
    {
        var method = typeof(MainWindow).GetMethod("ToggleCropMode", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((Task)method.Invoke(window, new object?[] { (bool?)enabled, true })!).GetAwaiter().GetResult();
    }

    private static void InvokeZoom(MainWindow window, Point origin, double delta)
    {
        var method = typeof(MainWindow).GetMethod("Zoom", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((Task)method.Invoke(window, new object?[] { origin, (double?)delta, null })!).GetAwaiter().GetResult();
    }

    private static void InvokePan(MainWindow window, Vector delta)
    {
        var method = typeof(MainWindow).GetMethod("ClampTransformToIntuitiveBounds", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(window, new object?[] { (Vector?)delta });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            throw new Xunit.Sdk.XunitException($"STA crop-mode test failed: {failure}");
    }
}
