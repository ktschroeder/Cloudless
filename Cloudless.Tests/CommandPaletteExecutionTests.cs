using System.Windows;
using System.Windows.Controls;
using System.Collections.Specialized;
using System.IO;
using Cloudless.PluginBase;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public class CommandPaletteExecutionTests
{
    [Fact]
    public void ExecuteCommand_ParsesSafeCommandsThroughPaletteEntryPoint()
    {
        RunOnStaThread(() =>
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ThemeManager.ApplyThemeGlobalResources("Dark");
            var darkBackground = (System.Windows.Media.Color)app.Resources["WindowBackgroundColor"];
            Assert.Equal((byte)0x1E, darkBackground.R);
            ThemeManager.ApplyThemeGlobalResources(string.Empty);
            var lightBackground = (System.Windows.Media.Color)app.Resources["WindowBackgroundColor"];
            Assert.Equal((byte)0xF9, lightBackground.R);
            var originalDisplayMode = Cloudless.Properties.Settings.Default.DisplayMode;
            var originalSortOrder = Cloudless.Properties.Settings.Default.ImageDirectorySortOrder;
            var originalDisableSmartZoom = Cloudless.Properties.Settings.Default.DisableSmartZoom;
            var originalCurrentPage = Cloudless.Properties.Settings.Default.CurrentPage;
            var originalRecentWorkspaces = Cloudless.Properties.Settings.Default.RecentWorkspaces?.Cast<string>().ToArray() ?? Array.Empty<string>();
            var originalUserCommands = Cloudless.Properties.Settings.Default.UserCommands?.Cast<string>().ToArray() ?? Array.Empty<string>();
            MainWindow? window = null;
            string? workspacePath = null;
            string? renamedWorkspacePath = null;
            string? tagForCleanup = null;
            string? unsupportedMediaPath = null;
            string? oversizedImageDirectory = null;
            string? navigationImagesDirectory = null;
            var macroPath = Path.Combine(MainWindow.workspaceFilesPath, "user_macros.txt");
            var originalMacros = File.Exists(macroPath) ? File.ReadAllBytes(macroPath) : null;
            string? macroName = null;
            var recentFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cloudless", "recent_files.json");
            var originalRecentFiles = File.Exists(recentFilesPath) ? File.ReadAllBytes(recentFilesPath) : null;
            var videoPositionsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cloudless", "video_positions.json");
            var originalVideoPositions = File.Exists(videoPositionsPath) ? File.ReadAllBytes(videoPositionsPath) : null;

            try
            {
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                int galleryCountBeforeInvalidPreview = Application.Current.Windows.OfType<GalleryWindow>().Count();
                window.ExecuteCommand("fs p").GetAwaiter().GetResult();
                Assert.Equal("A workspace name is required for a filmstrip preview.", window.LastMessage);
                Assert.Equal(galleryCountBeforeInvalidPreview, Application.Current.Windows.OfType<GalleryWindow>().Count());

                window.ExecuteCommand(" DM 1 ; dm zoom ; dm best ; dm 4 ").GetAwaiter().GetResult();
                Assert.Equal("BestFitWithoutZooming", Cloudless.Properties.Settings.Default.DisplayMode);

                var commands = new[]
                {
                    "dm stretch",
                    "dim",
                    "echo palette parsing",
                    "all echo all windows",
                    "others echo other windows",
                    "macro list",
                    "macro run missing-test-macro",
                    "macro delete missing-test-macro",
                    "set start",
                    "clear start",
                    "set end",
                    "clear end",
                    "set trigger invalid",
                    "clear trigger",
                    "audio sync",
                    "audio sync 120ms",
                    "subtitle sync",
                    "subtitle sync -0.25",
                    "sync",
                    "unsync",
                    "set flag",
                    "clear flag",
                    "goto flag",
                    "/no-match",
                    "r",
                    "r 1",
                    "first",
                    "last",
                    "sort name asc",
                    "sort name desc",
                    "sort date asc",
                    "sort date desc",
                    "ws origin",
                    "ws origin s",
                    "ws origin l",
                    "ws list",
                    "goto start",
                    "goto end",
                    "o Z:\\cloudless-command-test-missing.png",
                    "o! Z:\\cloudless-command-test-missing.png",
                    "dim",
                    "cip",
                    "mute",
                    "unmute",
                    "speed",
                    "speed 2",
                    "volume",
                    "volume 50",
                    "rev",
                    "play",
                    "pause",
                    "seek ?",
                    "seek 10",
                    "p ?",
                    "page ?",
                    "flatten",
                    "ss stop",
                    "ss invalid",
                    "tag",
                    "tag list",
                    "t l",
                    "fs tag no-such-palette-tag",
                    "fs t no-such-palette-tag",
                    "filmstrip tag no-such-palette-tag",
                    "filmstrip t no-such-palette-tag",
                    "open tag no-such-palette-tag",
                    "open t no-such-palette-tag",
                    "o tag no-such-palette-tag",
                    "o t no-such-palette-tag",
                    "open! tag no-such-palette-tag",
                    "open! t no-such-palette-tag",
                    "o! tag no-such-palette-tag",
                    "o! t no-such-palette-tag",
                    "gallery tag no-such-palette-tag",
                    "gallery t no-such-palette-tag",
                    "tag destroy no-such-palette-tag",
                    "1",
                    "+1",
                    "not-a-command"
                };

                window.ExecuteCommand("c24 set echo custom-command-ran").GetAwaiter().GetResult();
                window.ExecuteCommand("c24 view").GetAwaiter().GetResult();
                window.ExecuteCommand("c24 run").GetAwaiter().GetResult();
                Assert.Contains("echo custom-command-ran", window.CommandHistory.TakeLast(2));

                foreach (var command in commands)
                    window.ExecuteCommand(command).GetAwaiter().GetResult();

                Assert.Equal(
                    new[] { "gallery t no-such-palette-tag", "tag destroy no-such-palette-tag", "1", "+1", "not-a-command" },
                    window.CommandHistory.TakeLast(5));
                Assert.Equal("Command not recognized", window.LastMessage);

                var incompleteCommands = new Dictionary<string, string>
                {
                    ["goto"] = "Usage: goto start, goto end, or goto flag",
                    ["set"] = "Usage: set start, set end, set flag, or set trigger [count]",
                    ["clear"] = "Usage: clear start, clear end, clear flag, or clear trigger",
                    ["seek"] = "Usage: seek previous, seek ?, or seek [time] (for example, seek 1:30)",
                    ["audio"] = "Usage: audio sync [offset]; use 'audio sync' to show the current offset",
                    ["subtitle"] = "Usage: subtitle sync [offset]; use 'subtitle sync' to show the current offset",
                    ["nudge"] = "Usage: nudge left|right|up|down [count]  OR  nudge <x> <y>",
                    ["sort"] = "Usage: sort name|date asc|desc",
                    ["dm"] = "Usage: dm stretch|zoom|best|bestnozoom (or dm 1-4)",
                    ["tag"] = "Usage: tag add [tags], tag remove [tags], tag destroy [tag], or tag list",
                    ["macro"] = "Usage: macro list, macro run [name], macro delete [name], or macro record [name] [command]",
                    ["p"] = "Usage: p [target], p arrange, or p [target] send|bring|clear|swap ...",
                    ["page"] = "Usage: p [target], p arrange, or p [target] send|bring|clear|swap ...",
                    ["slideshow"] = "Usage: slideshow [seconds] [shuffle] [triggers], slideshow triggers, slideshow stop, or slideshow next",
                    ["ws"] = "Usage: ws save|save!|load|merge|preview|rename|delete|origin|list ...",
                    ["all"] = "Usage: all [command]",
                    ["others"] = "Usage: others [command]",
                    ["c24"] = "Usage: c24 set [command], c24 view, or c24 run"
                };

                foreach (var (command, expectedMessage) in incompleteCommands)
                {
                    window.ExecuteCommand(command).GetAwaiter().GetResult();
                    Assert.Equal(expectedMessage, window.LastMessage);
                }

                var workspaceName = "palette-test-" + Guid.NewGuid().ToString("N");
                workspacePath = Path.Combine(MainWindow.workspaceFilesPath, workspaceName + ".cloudless");
                var renamedWorkspaceName = workspaceName + "-renamed";
                renamedWorkspacePath = Path.Combine(MainWindow.workspaceFilesPath, renamedWorkspaceName + ".cloudless");
                window.ExecuteCommand("ws s " + workspaceName).GetAwaiter().GetResult();
                Assert.True(File.Exists(workspacePath));
                window.ExecuteCommand("ws s! " + workspaceName).GetAwaiter().GetResult();
                window.ExecuteCommand("ws r " + workspaceName + " " + renamedWorkspaceName).GetAwaiter().GetResult();
                Assert.True(File.Exists(renamedWorkspacePath));
                window.ExecuteCommand("ws delete " + renamedWorkspaceName).GetAwaiter().GetResult();
                workspacePath = null;
                Assert.False(File.Exists(renamedWorkspacePath));

                var videoHost = window.FindName("VideoHost") as ContentControl
                    ?? (ContentControl?)typeof(MainWindow)
                        .GetField("VideoHost", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                        ?.GetValue(window);
                Assert.NotNull(videoHost);
                var imagePathField = typeof(MainWindow).GetField(
                    "currentlyDisplayedImagePath",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(imagePathField);

                var player = new FakeVideoPlayer { Position = TimeSpan.FromSeconds(60) };
                videoHost!.Content = player;
                imagePathField!.SetValue(window, @"C:\palette-test.mp4");

                var resumePath = Path.Combine(Path.GetTempPath(), "cloudless-resume-test.mp4");
                var savedResumePosition = TimeSpan.FromSeconds(23);
                VideoPlaybackPositionStore.SavePosition(resumePath, savedResumePosition);
                var storedResumePosition = VideoPlaybackPositionStore.GetPosition(resumePath);
                Assert.Equal(savedResumePosition, storedResumePosition);
                var resumeMethod = typeof(MainWindow).GetMethod(
                    "PlayVideoWithOptionalResumeAsync",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(resumeMethod);
                var resumeTask = resumeMethod!.Invoke(window, new object?[]
                {
                    player,
                    new Uri(resumePath),
                    null,
                    storedResumePosition
                }) as Task;
                Assert.NotNull(resumeTask);
                resumeTask!.GetAwaiter().GetResult();
                Assert.Equal(savedResumePosition, player.Position);

                tagForCleanup = "palette-test-" + Guid.NewGuid().ToString("N");
                window.ExecuteCommand("tag add " + tagForCleanup).GetAwaiter().GetResult();
                Assert.Contains(tagForCleanup, TagManager.Instance.GetTagsForFile(@"C:\palette-test.mp4"));
                window.ExecuteCommand("t r " + tagForCleanup).GetAwaiter().GetResult();
                Assert.DoesNotContain(tagForCleanup, TagManager.Instance.GetTagsForFile(@"C:\palette-test.mp4"));
                window.ExecuteCommand("t a " + tagForCleanup).GetAwaiter().GetResult();
                window.ExecuteCommand("tag destroy " + tagForCleanup).GetAwaiter().GetResult();
                Assert.DoesNotContain(tagForCleanup, TagManager.Instance.GetTagsForFile(@"C:\palette-test.mp4"));

                macroName = "palette-test-" + Guid.NewGuid().ToString("N");
                window.ExecuteCommand("macro record " + macroName + " echo macro-command-ran").GetAwaiter().GetResult();
                window.ExecuteCommand("macro run " + macroName).GetAwaiter().GetResult();
                Assert.Contains("echo macro-command-ran", window.CommandHistory.TakeLast(2));
                window.ExecuteCommand("macro list").GetAwaiter().GetResult();
                window.ExecuteCommand("macro delete " + macroName).GetAwaiter().GetResult();

                window.ExecuteCommand("ss 60 shuffle").GetAwaiter().GetResult();
                window.ExecuteCommand("ss next").GetAwaiter().GetResult();
                window.ExecuteCommand("ss stop").GetAwaiter().GetResult();

                window.ExecuteCommand("seek 1:30").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(90), player.Position);
                window.ExecuteCommand("seek -30").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(60), player.Position);
                window.ExecuteCommand("seek -90").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.Zero, player.Position);
                window.ExecuteCommand("seek +400").GetAwaiter().GetResult();
                Assert.Equal(player.Duration, player.Position);

                player.Duration = TimeSpan.FromHours(2);
                window.ExecuteCommand("seek 1:30:45").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(5_445), player.Position);
                window.ExecuteCommand("seek 1h30m45s").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(5_445), player.Position);
                player.Duration = TimeSpan.FromMinutes(5);
                window.ExecuteCommand("seek 301").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(5_445), player.Position);
                Assert.StartsWith("Seek time", window.LastMessage);
                window.ExecuteCommand("seek +not-a-time").GetAwaiter().GetResult();
                Assert.Contains("Could not parse relative time", window.LastMessage);
                player.Position = TimeSpan.FromSeconds(60);

                window.ExecuteCommand("set start").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(60), window.VideoLoopStart);
                window.ExecuteCommand("seek 1:45").GetAwaiter().GetResult();
                window.ExecuteCommand("set end").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(105), window.VideoLoopEnd);
                window.ExecuteCommand("goto start").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(60), player.Position);
                window.ExecuteCommand("goto end").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(105), player.Position);

                window.ExecuteCommand("set f").GetAwaiter().GetResult();
                window.ExecuteCommand("seek 10").GetAwaiter().GetResult();
                window.ExecuteCommand("goto f").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(105), player.Position);
                window.ExecuteCommand("seek 10").GetAwaiter().GetResult();
                window.ExecuteCommand("goto flag").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(105), player.Position);
                window.ExecuteCommand("clear flag").GetAwaiter().GetResult();

                window.ExecuteCommand("audio sync 120ms").GetAwaiter().GetResult();
                Assert.Equal(120_000, player.AudioDelay);
                window.ExecuteCommand("subtitle sync -0.25").GetAwaiter().GetResult();
                Assert.Equal(-250_000, player.SubtitleDelay);

                window.ExecuteCommand("speed 1.5").GetAwaiter().GetResult();
                Assert.Equal(1.5, player.Speed);
                window.ExecuteCommand("volume 55").GetAwaiter().GetResult();
                Assert.Equal(55, player.Volume);

                window.ExecuteCommand("play").GetAwaiter().GetResult();
                Assert.False(player.Paused);
                window.ExecuteCommand("pause").GetAwaiter().GetResult();
                Assert.True(player.Paused);

                window.ExecuteCommand("sync").GetAwaiter().GetResult();
                Assert.True(window.VideoIsSynced);
                Assert.False(player.AutoRestartAllowed);
                window.ExecuteCommand("unsync").GetAwaiter().GetResult();
                Assert.False(window.VideoIsSynced);
                Assert.True(player.AutoRestartAllowed);

                window.ExecuteCommand("set trigger 2").GetAwaiter().GetResult();
                Assert.Equal(2, window.SlideshowTriggerCount);
                window.ExecuteCommand("clear trigger").GetAwaiter().GetResult();
                Assert.Equal(0, window.SlideshowTriggerCount);

                window.ExecuteCommand("hotkey ctrl right").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(110), player.Position);
                window.ExecuteCommand("hk control left").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(105), player.Position);

                window.ExecuteCommand("hotkey ctrl definitely-not-a-key").GetAwaiter().GetResult();
                Assert.Equal(
                    "Failed to parse hotkey name: 'definitely-not-a-key'. Try names like 'left', 'space', 'f1' or a single character.",
                    window.LastMessage);
                window.ExecuteCommand("hotkey ctrl").GetAwaiter().GetResult();
                Assert.Equal("Command failed: Expected exactly one key token after any modifiers (ctrl alt shift)", window.LastMessage);

                window.ExecuteCommand("dm invalid").GetAwaiter().GetResult();
                Assert.Equal("Usage: dm stretch|zoom|best|bestnozoom (or dm 1-4)", window.LastMessage);
                window.ExecuteCommand("volume 101").GetAwaiter().GetResult();
                Assert.Equal("Volume must be from 0 to 100.", window.LastMessage);
                window.ExecuteCommand("speed 0").GetAwaiter().GetResult();
                Assert.Equal("Invalid playback speed. Provide a positive multiplier, e.g. 'speed 2.5'.", window.LastMessage);
                window.ExecuteCommand("set trigger 0").GetAwaiter().GetResult();
                Assert.Equal("Trigger count must be a positive integer", window.LastMessage);
                window.ExecuteCommand("seek not-a-time").GetAwaiter().GetResult();
                Assert.StartsWith("Could not parse time 'not-a-time'.", window.LastMessage);

                Assert.Equal("StretchToFit", Cloudless.Properties.Settings.Default.DisplayMode);
                Assert.Equal(
                    new[] { "hotkey ctrl definitely-not-a-key", "hotkey ctrl", "dm invalid", "volume 101", "speed 0", "set trigger 0", "seek not-a-time" },
                    window.CommandHistory.TakeLast(7));

                var imageDisplay = (System.Windows.Controls.Image?)typeof(MainWindow)
                    .GetField("ImageDisplay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                    ?.GetValue(window);
                Assert.NotNull(imageDisplay);

                foreach (var (assetName, expectedWidth, expectedHeight) in new[]
                {
                    ("img1-landscape.jpg", 640, 427),
                    ("img2-portrait.jpg", 640, 963),
                    ("img3-portrait.jpg", 640, 960),
                    ("img4-portrait.jpg", 640, 963)
                })
                {
                    var imagePath = Path.Combine(AppContext.BaseDirectory, "TestAssets", assetName);
                    window.ExecuteCommand("o " + imagePath).GetAwaiter().GetResult();

                    Assert.Equal(imagePath, imagePathField.GetValue(window)?.ToString(), StringComparer.OrdinalIgnoreCase);
                    var displayedBitmap = Assert.IsAssignableFrom<System.Windows.Media.Imaging.BitmapSource>(imageDisplay!.Source);
                    Assert.Equal(expectedWidth, displayedBitmap.PixelWidth);
                    Assert.Equal(expectedHeight, displayedBitmap.PixelHeight);
                }

                unsupportedMediaPath = Path.Combine(Path.GetTempPath(), "cloudless-command-test-" + Guid.NewGuid().ToString("N") + ".txt");
                File.WriteAllText(unsupportedMediaPath, "not an image");
                window.ExecuteCommand("o " + unsupportedMediaPath).GetAwaiter().GetResult();
                Assert.StartsWith("Image not found at path:", window.LastMessage);

                navigationImagesDirectory = Path.Combine(Path.GetTempPath(), "cloudless-navigation-test-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(navigationImagesDirectory);
                var firstNavigationImage = Path.Combine(navigationImagesDirectory, "01.jpg");
                var secondNavigationImage = Path.Combine(navigationImagesDirectory, "02.jpg");
                File.Copy(Path.Combine(AppContext.BaseDirectory, "TestAssets", "img1-landscape.jpg"), firstNavigationImage);
                File.Copy(Path.Combine(AppContext.BaseDirectory, "TestAssets", "img2-portrait.jpg"), secondNavigationImage);
                Cloudless.Properties.Settings.Default.ImageDirectorySortOrder = "FileNameAscending";
                window.ExecuteCommand("o " + firstNavigationImage).GetAwaiter().GetResult();
                Assert.Equal(firstNavigationImage, imagePathField.GetValue(window)?.ToString(), StringComparer.OrdinalIgnoreCase);

                oversizedImageDirectory = Path.Combine(Path.GetTempPath(), "cloudless-command-test-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(oversizedImageDirectory);
                for (var i = 0; i < 11; i++)
                    File.WriteAllBytes(Path.Combine(oversizedImageDirectory, $"image-{i}.jpg"), Array.Empty<byte>());
                window.ExecuteCommand("o " + oversizedImageDirectory).GetAwaiter().GetResult();
                Assert.Equal(
                    "Your command would open more than 10 images (11) and was stopped. To override this, use: 'o! [directory]'",
                    window.LastMessage);

                window.Show();
                window.UpdateLayout();
                imageDisplay!.UpdateLayout();
                Assert.True(window.IsVisible);
                Assert.NotNull(System.Windows.PresentationSource.FromVisual(window));
                Assert.True(imageDisplay.IsVisible);
                Assert.True(imageDisplay.ActualWidth > 0);
                Assert.True(imageDisplay.ActualHeight > 0);

                var renderedImage = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    (int)Math.Ceiling(imageDisplay.ActualWidth),
                    (int)Math.Ceiling(imageDisplay.ActualHeight),
                    96,
                    96,
                    System.Windows.Media.PixelFormats.Pbgra32);
                renderedImage.Render(imageDisplay);
                var renderedPixels = new byte[renderedImage.PixelWidth * renderedImage.PixelHeight * 4];
                renderedImage.CopyPixels(renderedPixels, renderedImage.PixelWidth * 4, 0);
                var hasVisiblePixels = false;
                for (var i = 3; i < renderedPixels.Length; i += 4)
                {
                    if (renderedPixels[i] != 0)
                    {
                        hasVisiblePixels = true;
                        break;
                    }
                }
                Assert.True(hasVisiblePixels);

                RaiseKeyAndWaitForImage(window, System.Windows.Input.Key.Right, imagePathField, secondNavigationImage);
                AssertImageDimensions(imageDisplay, 640, 963);
                RaiseKeyAndWaitForImage(window, System.Windows.Input.Key.Left, imagePathField, firstNavigationImage);
                AssertImageDimensions(imageDisplay, 640, 427);
                RaiseKeyAndWaitForImage(window, System.Windows.Input.Key.Left, imagePathField, secondNavigationImage);
                AssertImageDimensions(imageDisplay, 640, 963);
                RaiseKeyAndWaitForImage(window, System.Windows.Input.Key.Right, imagePathField, firstNavigationImage);
                AssertImageDimensions(imageDisplay, 640, 427);

                Cloudless.Properties.Settings.Default.DisableSmartZoom = false;
                Assert.Equal(1, window.imageScaleTransform!.ScaleX);
                window.SimulateKeyEvent(System.Windows.Input.Key.OemPlus, false, true, false).GetAwaiter().GetResult();
                Assert.Equal(1.1, window.imageScaleTransform.ScaleX, 5);
                Assert.Equal(1.1, window.imageScaleTransform.ScaleY, 5);
                window.SimulateKeyEvent(System.Windows.Input.Key.OemMinus, false, true, false).GetAwaiter().GetResult();
                Assert.Equal(1, window.imageScaleTransform.ScaleX, 5);
                Assert.Equal(1, window.imageScaleTransform.ScaleY, 5);

                window.SimulateKeyEvent(System.Windows.Input.Key.OemPlus, false, true, false).GetAwaiter().GetResult();
                var originalPanX = window.imageTranslateTransform!.X;
                var originalPanY = window.imageTranslateTransform.Y;
                RaisePanMouseMove(window, new System.Windows.Vector(5, 0));
                Assert.NotEqual(originalPanX, window.imageTranslateTransform.X);
                Assert.Equal(originalPanY, window.imageTranslateTransform.Y, 5);
                window.SimulateKeyEvent(System.Windows.Input.Key.D0, false, true, false).GetAwaiter().GetResult();
                Assert.Equal(1, window.imageScaleTransform.ScaleX, 5);
                Assert.Equal(0, window.imageTranslateTransform.X, 5);
                Assert.Equal(0, window.imageTranslateTransform.Y, 5);

                var windowWidthBeforeCrop = window.Width;
                var windowHeightBeforeCrop = window.Height;
                window.EnterSelectionMode().GetAwaiter().GetResult();
                var imageBounds = imageDisplay.TransformToAncestor(window).TransformBounds(
                    new System.Windows.Rect(0, 0, imageDisplay.ActualWidth, imageDisplay.ActualHeight));
                var selectionStart = new System.Windows.Point(
                    imageBounds.Left + imageBounds.Width * 0.25,
                    imageBounds.Top + imageBounds.Height * 0.25);
                var selectionEnd = new System.Windows.Point(
                    imageBounds.Left + imageBounds.Width * 0.75,
                    imageBounds.Top + imageBounds.Height * 0.75);
                window.SelectionMode_MouseDown(selectionStart);
                window.SelectionMode_MouseMove(selectionEnd);
                window.SelectionMode_MouseUp(selectionEnd).GetAwaiter().GetResult();
                Assert.Equal("Crop applied to selection", window.LastMessage);
                Assert.True(window.Width < windowWidthBeforeCrop);
                Assert.True(window.Height < windowHeightBeforeCrop);

            }
            finally
            {
                Cloudless.Properties.Settings.Default.DisplayMode = originalDisplayMode;
                Cloudless.Properties.Settings.Default.ImageDirectorySortOrder = originalSortOrder;
                Cloudless.Properties.Settings.Default.DisableSmartZoom = originalDisableSmartZoom;
                Cloudless.Properties.Settings.Default.CurrentPage = originalCurrentPage;
                var recentWorkspaces = new StringCollection();
                recentWorkspaces.AddRange(originalRecentWorkspaces);
                Cloudless.Properties.Settings.Default.RecentWorkspaces = recentWorkspaces;
                var userCommands = new StringCollection();
                userCommands.AddRange(originalUserCommands);
                Cloudless.Properties.Settings.Default.UserCommands = userCommands;
                Cloudless.Properties.Settings.Default.Save();
                if (workspacePath != null && File.Exists(workspacePath))
                    File.Delete(workspacePath);
                if (renamedWorkspacePath != null && File.Exists(renamedWorkspacePath))
                    File.Delete(renamedWorkspacePath);
                if (unsupportedMediaPath != null && File.Exists(unsupportedMediaPath))
                    File.Delete(unsupportedMediaPath);
                if (oversizedImageDirectory != null && Directory.Exists(oversizedImageDirectory))
                    Directory.Delete(oversizedImageDirectory, recursive: true);
                if (originalMacros != null)
                    File.WriteAllBytes(macroPath, originalMacros);
                else if (File.Exists(macroPath))
                    File.Delete(macroPath);
                if (tagForCleanup != null)
                    TagManager.Instance.DestroyTag(tagForCleanup);
                window?.Close();
                if (navigationImagesDirectory != null && Directory.Exists(navigationImagesDirectory))
                    Directory.Delete(navigationImagesDirectory, recursive: true);
                app.Shutdown();
                RestoreFile(recentFilesPath, originalRecentFiles);
                RestoreFile(videoPositionsPath, originalVideoPositions);
            }
        });
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
            throw new Xunit.Sdk.XunitException($"STA command-palette test failed: {failure}");
    }

    private static void RaiseKeyAndWaitForImage(
        MainWindow window,
        System.Windows.Input.Key key,
        System.Reflection.FieldInfo imagePathField,
        string expectedPath)
    {
        var presentationSource = System.Windows.PresentationSource.FromVisual(window)
            ?? throw new InvalidOperationException("The test window has no presentation source.");
        var keyEvent = new System.Windows.Input.KeyEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice,
            presentationSource,
            Environment.TickCount,
            key)
        {
            RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent
        };
        window.RaiseEvent(keyEvent);

        var frame = new System.Windows.Threading.DispatcherFrame();
        var completed = false;
        var checkTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background,
            window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(20)
        };
        var timeoutTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background,
            window.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(5)
        };

        checkTimer.Tick += (_, _) =>
        {
            if (string.Equals(imagePathField.GetValue(window) as string, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                completed = true;
                frame.Continue = false;
            }
        };
        timeoutTimer.Tick += (_, _) => frame.Continue = false;
        checkTimer.Start();
        timeoutTimer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        checkTimer.Stop();
        timeoutTimer.Stop();

        Assert.True(completed, $"The {key} key did not navigate to '{expectedPath}'.");
    }

    private static void AssertImageDimensions(System.Windows.Controls.Image imageDisplay, int expectedWidth, int expectedHeight)
    {
        var bitmap = Assert.IsAssignableFrom<System.Windows.Media.Imaging.BitmapSource>(imageDisplay.Source);
        Assert.Equal(expectedWidth, bitmap.PixelWidth);
        Assert.Equal(expectedHeight, bitmap.PixelHeight);
    }

    private static void RaisePanMouseMove(MainWindow window, System.Windows.Vector delta)
    {
        var isPanningImageField = typeof(MainWindow).GetField(
            "isPanningImage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var lastMousePositionField = typeof(MainWindow).GetField(
            "lastMousePosition",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(isPanningImageField);
        Assert.NotNull(lastMousePositionField);

        var currentPosition = System.Windows.Input.Mouse.GetPosition(window);
        lastMousePositionField!.SetValue(window, new System.Windows.Point(
            currentPosition.X - delta.X,
            currentPosition.Y - delta.Y));
        isPanningImageField!.SetValue(window, true);
        try
        {
            window.RaiseEvent(new System.Windows.Input.MouseEventArgs(
                System.Windows.Input.Mouse.PrimaryDevice,
                Environment.TickCount)
            {
                RoutedEvent = System.Windows.Input.Mouse.MouseMoveEvent
            });
        }
        finally
        {
            isPanningImageField.SetValue(window, false);
        }
    }

    private static void WaitForDispatcherTask(Task task, System.Windows.Threading.Dispatcher dispatcher, string operation)
    {
        if (!task.IsCompleted)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timedOut = false;
            var timeoutTimer = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Background,
                dispatcher)
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            timeoutTimer.Tick += (_, _) =>
            {
                timedOut = true;
                frame.Continue = false;
            };
            task.ContinueWith(
                _ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
                TaskScheduler.Default);
            timeoutTimer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            timeoutTimer.Stop();

            if (timedOut && !task.IsCompleted)
                throw new TimeoutException($"Timed out waiting for {operation}.");
        }

        task.GetAwaiter().GetResult();
    }

    private static void WaitForDispatcherCondition(
        Func<bool> condition,
        System.Windows.Threading.Dispatcher dispatcher,
        string operation)
    {
        if (condition())
            return;

        var frame = new System.Windows.Threading.DispatcherFrame();
        var completed = false;
        var checkTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background,
            dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        var timeoutTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background,
            dispatcher)
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        checkTimer.Tick += (_, _) =>
        {
            if (condition())
            {
                completed = true;
                frame.Continue = false;
            }
        };
        timeoutTimer.Tick += (_, _) => frame.Continue = false;
        checkTimer.Start();
        timeoutTimer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        checkTimer.Stop();
        timeoutTimer.Stop();

        Assert.True(completed, $"Timed out waiting for {operation}.");
    }

    private static void RestoreFile(string path, byte[]? originalContents)
    {
        if (originalContents == null)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, originalContents);
    }
}

[CollectionDefinition("WPF command palette", DisableParallelization = true)]
public sealed class WpfCommandPaletteCollection;

internal sealed class FakeVideoPlayer : UserControl, IVideoPlayer
{
    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan? LoopStart { get; private set; }
    public TimeSpan? LoopEnd { get; private set; }
    public long AudioDelay { get; private set; }
    public long SubtitleDelay { get; private set; }
    public double Speed { get; private set; } = 1;
    public double Volume { get; private set; } = 100;
    public bool Paused { get; private set; } = true;
    public bool Muted { get; private set; }
    public bool AutoRestartAllowed { get; private set; } = true;
    public int FineSeekForwardCount { get; private set; }
    public int FineSeekBackwardCount { get; private set; }
    private EventHandler<VideoTimeChangedEventArgs>? _timeChanged;
    public event EventHandler<VideoTimeChangedEventArgs>? TimeChanged
    {
        add => _timeChanged += value;
        remove => _timeChanged -= value;
    }

    public Task Play(Uri uri, Task? postPlayTask = null) => Task.CompletedTask;
    public void RaiseTimeChanged(long timeMilliseconds) => _timeChanged?.Invoke(this, new VideoTimeChangedEventArgs { TimeMilliseconds = timeMilliseconds });
    public void TogglePause(bool? setTo = null) => Paused = setTo ?? !Paused;
    public void Stop() { }
    public void SetMedia(Uri uri) { }
    public Task<(int, int)?> GetDimensions() => Task.FromResult<(int, int)?>(null);
    public void Dispose() { }
    public void Restart() => Position = TimeSpan.Zero;
    public TimeSpan GetDuration() => Duration;
    public TimeSpan GetPosition() => Position;
    public void SeekTo(TimeSpan position) => Position = position;
    public void SetLoopRange(TimeSpan? start, TimeSpan? end) { LoopStart = start; LoopEnd = end; }
    public void SeekFineForward() => FineSeekForwardCount++;
    public void SeekFineBackward() => FineSeekBackwardCount++;
    public void Mute() => Muted = true;
    public void Unmute() => Muted = false;
    public bool IsMuted() => Muted;
    public bool IsPaused() => Paused;
    public bool HasAudio() => true;
    public IReadOnlyList<MediaTrackInfo> GetAudioTracks() => Array.Empty<MediaTrackInfo>();
    public IReadOnlyList<MediaTrackInfo> GetSubtitleTracks() => Array.Empty<MediaTrackInfo>();
    public int GetCurrentAudioTrackId() => -1;
    public int GetCurrentSubtitleTrackId() => -1;
    public bool SetAudioTrack(int trackId) => false;
    public bool SetSubtitleTrack(int trackId) => false;
    public long GetAudioDelay() => AudioDelay;
    public long GetSubtitleDelay() => SubtitleDelay;
    public bool SetAudioDelay(long delayMicroseconds) { AudioDelay = delayMicroseconds; return true; }
    public bool SetSubtitleDelay(long delayMicroseconds) { SubtitleDelay = delayMicroseconds; return true; }
    public double GetPlaybackSpeed() => Speed;
    public bool SetPlaybackSpeed(double speed) { Speed = speed; return true; }
    public void SetVolume(double volume) => Volume = volume;
    public double GetVolume() => Volume;
    public void SetVideoZoom(double scale, double centerX, double centerY, bool constrainPan) { }
    public void PanVideoBy(double deltaX, double deltaY, bool constrainToBounds) { }
    public void SetVideoCropMode(bool enabled) { }
    public double GetVideoZoom() => 1;
    public (double, double) GetVideoPan() => (0, 0);
    public void ResetVideoPanZoom() { }
    public void SetAutoRestartAllowed(bool allowed) => AutoRestartAllowed = allowed;
    public TimeSpan? GetCustomEnd() => LoopEnd;
}
