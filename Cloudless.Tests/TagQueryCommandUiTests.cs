using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class TagQueryCommandUiTests
{
    [Fact]
    public void OpenTagCommandUsesBooleanQueriesAndDistinguishesMissingFilesFromNoMatches()
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
            var recentFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cloudless", "recent_files.json");
            var originalRecentFiles = File.Exists(recentFilesPath) ? File.ReadAllBytes(recentFilesPath) : null;
            var directory = Path.Combine(Path.GetTempPath(), "cloudless-tag-command-" + Guid.NewGuid().ToString("N"));
            var tagPrefix = "command-test-" + Guid.NewGuid().ToString("N");
            var commonTag = tagPrefix + "-common";
            var matchingTag = tagPrefix + "-matching";
            var missingTag = tagPrefix + "-missing";
            var imageWithBothTags = Path.Combine(directory, "both.jpg");
            var imageWithCommonTag = Path.Combine(directory, "common-only.jpg");
            var missingImage = Path.Combine(directory, "not-on-disk.jpg");
            MainWindow? window = null;
            try
            {
                Directory.CreateDirectory(directory);
                var sampleImage = Path.Combine(AppContext.BaseDirectory, "TestAssets", "img1-landscape.jpg");
                File.Copy(sampleImage, imageWithBothTags);
                File.Copy(sampleImage, imageWithCommonTag);
                var tags = TagManager.Instance;
                tags.AddTags(imageWithBothTags, commonTag, matchingTag);
                tags.AddTags(imageWithCommonTag, commonTag);
                tags.AddTags(missingImage, missingTag);

                settings.CurrentPage = 1;
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();
                var previousContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                try
                {
                    window.ExecuteCommand($"open tag {commonTag} AND {matchingTag}").GetAwaiter().GetResult();
                    Assert.Equal(imageWithBothTags, GetImagePath(window), StringComparer.OrdinalIgnoreCase);
                    Assert.Equal($"Opened 1 file matching query '{commonTag} and {matchingTag}'.", window.LastMessage);
                    Assert.Single(Application.Current.Windows.OfType<MainWindow>());

                    window.ExecuteCommand($"open tag {missingTag}").GetAwaiter().GetResult();
                    Assert.Equal($"tag open: Query matched 1 file(s), but none exist on disk.", window.LastMessage);
                    Assert.Equal(imageWithBothTags, GetImagePath(window), StringComparer.OrdinalIgnoreCase);

                    var absentTag = tagPrefix + "-absent";
                    window.ExecuteCommand($"open tag {absentTag}").GetAwaiter().GetResult();
                    Assert.Equal($"tag open: No files found matching query '{absentTag}'.", window.LastMessage);
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
                TagManager.Instance.DestroyTag(commonTag);
                TagManager.Instance.DestroyTag(matchingTag);
                TagManager.Instance.DestroyTag(missingTag);
                settings.CurrentPage = originalCurrentPage;
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

    private static string GetImagePath(MainWindow window)
    {
        var field = typeof(MainWindow).GetField("currentlyDisplayedImagePath", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The current media path field was not found.");
        return (string)field.GetValue(window)!;
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
            throw new Xunit.Sdk.XunitException($"Tag query command UI test failed: {failure}");
    }
}
