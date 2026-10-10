using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class ImageNavigationCommandUiTests
{
    [Fact]
    public void IndexRelativeAndFilenameCommandsNavigateAndHandleBoundaries()
    {
        RunOnStaThread(() =>
        {
            ResetWpfApplicationSingletonForTestIsolation();
            App.SuppressStartupForUiTests = true;
            var app = new App();
            app.InitializeComponent();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ThemeManager.ApplyThemeGlobalResources("Dark");

            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            var originalSortOrder = settings.ImageDirectorySortOrder;
            var recentFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cloudless", "recent_files.json");
            var originalRecentFiles = File.Exists(recentFilesPath) ? File.ReadAllBytes(recentFilesPath) : null;
            var directory = Path.Combine(Path.GetTempPath(), "cloudless-navigation-" + Guid.NewGuid().ToString("N"));
            MainWindow? window = null;
            try
            {
                Directory.CreateDirectory(directory);
                var imageNames = new[] { "01-alpha.jpg", "02-beta.jpg", "03-gamma.jpg" };
                var modifiedDayByImage = new[] { 3, 1, 2 };
                for (var index = 0; index < imageNames.Length; index++)
                {
                    var imagePath = Path.Combine(directory, imageNames[index]);
                    File.Copy(Path.Combine(AppContext.BaseDirectory, "TestAssets", "img1-landscape.jpg"), imagePath);
                    File.SetLastWriteTimeUtc(imagePath, new DateTime(2020, 1, modifiedDayByImage[index], 0, 0, 0, DateTimeKind.Utc));
                }

                settings.CurrentPage = 1;
                settings.ImageDirectorySortOrder = "FileNameAscending";
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();
                var previousContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                try
                {
                    var firstPath = Path.Combine(directory, imageNames[0]);
                    ExecuteAndAssertPath(window, "o " + firstPath, firstPath);
                    ExecuteAndAssertPath(window, "3", Path.Combine(directory, imageNames[2]));
                    ExecuteAndAssertPath(window, "-1", Path.Combine(directory, imageNames[1]));
                    ExecuteAndAssertPath(window, "+1", Path.Combine(directory, imageNames[2]));

                    ExecuteAndAssertPath(window, "+99", Path.Combine(directory, imageNames[2]));
                    Assert.Equal("Relative jump was clamped to final image in directory", window.LastMessage);
                    ExecuteAndAssertPath(window, "-99", firstPath);
                    Assert.Equal("Relative jump was clamped to first image in directory", window.LastMessage);

                    ExecuteAndAssertPath(window, "0", firstPath);
                    Assert.Equal("You are in 1-indexing mode, so the first image has index 1, not 0.", window.LastMessage);
                    ExecuteAndAssertPath(window, "last", Path.Combine(directory, imageNames[2]));
                    ExecuteAndAssertPath(window, "first", firstPath);

                    ExecuteAndAssertPath(window, "3", Path.Combine(directory, imageNames[2]));
                    ExecuteAndAssertPath(window, "/ALPHA", firstPath);
                    Assert.Equal("Continuing search from start of directory", window.LastMessage);
                    ExecuteAndAssertPath(window, "/no-match", firstPath);
                    Assert.Equal("No match for \"no-match\"", window.LastMessage);

                    window.ExecuteCommand("sort name desc").GetAwaiter().GetResult();
                    ExecuteAndAssertPath(window, "first", Path.Combine(directory, imageNames[2]));
                    ExecuteAndAssertPath(window, "last", firstPath);
                    window.ExecuteCommand("sort date asc").GetAwaiter().GetResult();
                    ExecuteAndAssertPath(window, "first", Path.Combine(directory, imageNames[1]));
                    ExecuteAndAssertPath(window, "last", firstPath);
                    window.ExecuteCommand("sort date desc").GetAwaiter().GetResult();
                    ExecuteAndAssertPath(window, "first", firstPath);
                    ExecuteAndAssertPath(window, "last", Path.Combine(directory, imageNames[1]));
                    window.ExecuteCommand("sort name asc").GetAwaiter().GetResult();
                    ExecuteAndAssertPath(window, "first", firstPath);
                    ExecuteAndAssertPath(window, "last", Path.Combine(directory, imageNames[2]));
                    window.ExecuteCommand("sort invalid").GetAwaiter().GetResult();
                    Assert.Equal("Usage: sort name|date asc|desc", window.LastMessage);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
            finally
            {
                foreach (var openWindow in Application.Current.Windows.OfType<Window>().ToArray())
                    openWindow.Close();
                settings.CurrentPage = originalCurrentPage;
                settings.ImageDirectorySortOrder = originalSortOrder;
                settings.Save();
                app.Shutdown();
                App.SuppressStartupForUiTests = false;
                ResetWpfApplicationSingletonForTestIsolation();
                RestoreFile(recentFilesPath, originalRecentFiles);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        });
    }

    private static void ExecuteAndAssertPath(MainWindow window, string command, string expectedPath)
    {
        window.ExecuteCommand(command).GetAwaiter().GetResult();
        var pathField = typeof(MainWindow).GetField("currentlyDisplayedImagePath", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The current media path field was not found.");
        Assert.Equal(expectedPath, pathField.GetValue(window)?.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    private static void RestoreFile(string path, byte[]? contents)
    {
        if (contents == null)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, contents);
    }

    private static void ResetWpfApplicationSingletonForTestIsolation()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(Application).GetField("_appInstance", flags)!.SetValue(null, null);
        typeof(Application).GetField("_appCreatedInThisAppDomain", flags)!.SetValue(null, false);
        typeof(Application).GetField("_isShuttingDown", flags)!.SetValue(null, false);
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
            throw new Xunit.Sdk.XunitException($"Image navigation command UI test failed: {failure}");
    }
}
