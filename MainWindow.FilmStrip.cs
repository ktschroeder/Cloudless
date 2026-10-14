using System;
using System.IO;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows;

namespace Cloudless
{
    public partial class MainWindow
    {
        public static readonly RoutedUICommand ToggleFilmStripCommand = new RoutedUICommand("ToggleFilmStrip", "ToggleFilmStrip", typeof(MainWindow));

        private FilmStripWindow? _filmStripWindow;
        private CommandPaletteWindow? _commandPaletteWindow;
        private string[] _filmStripImages;

        private void ToggleFilmStrip_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ToggleFilmStrip();
        }

        // Refresh the film strip contents if visible. Safe to call from other parts of MainWindow.
        public void RefreshFilmStrip()
        {
            if (_filmStripWindow != null && _filmStripWindow.IsVisible)
            {
                var files = imageFiles ?? Array.Empty<string>();
                _ = _filmStripWindow.PopulateAsync(files, currentImageIndex, _preloadManager, IsFileBookmarked);
            }
        }

        internal bool ShouldNavigateFilmstripContents =>
            NonstandardFilmstrip && _filmStripWindow?.IsVisible == true && _filmStripImages?.Length > 0;

        private async Task<bool> TryNavigateNonstandardFilmstripAsync(int direction)
        {
            if (!ShouldNavigateFilmstripContents)
                return false;

            if (leftOrRightNavInProgress)
                return true;

            string? nextPath = FilmStripNavigation.GetAdjacentPath(
                _filmStripImages,
                currentlyDisplayedImagePath,
                direction);
            if (nextPath == null)
                return false;

            if (string.Equals(nextPath, currentlyDisplayedImagePath, StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                leftOrRightNavInProgress = true;
                await LoadImage(nextPath, openedThroughApp: true);
            }
            finally
            {
                leftOrRightNavInProgress = false;
            }

            return true;
        }

        public void ToggleFilmStrip(bool skipPopulation = false)
        {
            if (_filmStripWindow == null)
            {
                _filmStripWindow = new FilmStripWindow();
                _filmStripWindow.ThumbnailClicked += OnFilmStripThumbnailClicked;
                _filmStripWindow.BulkTagEditRequested += EditTagsForFiles;
                _filmStripWindow.BulkBookmarkEditRequested += EditBookmarksForFiles;
                _filmStripWindow.OpenSelectedInNewWindowsRequested += OpenSelectedMediaInNewWindowsAsync;
                _filmStripWindow.OpenSelectedOnPageRequested += OpenSelectedMediaOnPageAsync;
                _filmStripWindow.SendSelectedToFilmstripRequested += SendSelectedMediaToFilmStripAsync;
                _filmStripWindow.ImageDetailsRequested += ShowImageInfoForPath;
            }

            if (_filmStripWindow.IsVisible)
            {
                NonstandardFilmstrip = false;
                _filmStripWindow.Hide();
                _filmStripWindow.DetachOwnerHandlers(this);
            }
            else
            {
                // Reset the retain checkbox before showing the window
                // This ensures it starts in the correct state for explicit targets
                var filmStripControl = FilmStripControl.GetControlFromWindow(_filmStripWindow);
                if (filmStripControl != null)
                {
                    // Always reset to unchecked when showing the filmstrip
                    filmStripControl.ClearRetainCheckboxOnThisInstanceOnly();
                }

                // Align to owner and attach handlers so it follows owner movements
                double desiredHeight = 140;
                _filmStripWindow.AlignToOwner(this, desiredHeight);
                _filmStripWindow.AttachOwnerHandlers(this);
                _filmStripWindow.Show();

                // If this is an explicit target, the checkbox is already cleared above
                // If it's NOT an explicit target, we may need to check for retained contents
                if (!skipPopulation)
                {
                    // If retained contents exist, use them instead of directory contents
                    bool isUsingRetainedContents = FilmStripControl.HasRetainedContents;
                    if (isUsingRetainedContents)
                    {
                        _filmStripImages = FilmStripControl.GetRetainedContents ?? Array.Empty<string>();
                        NonstandardFilmstrip = true;
                    }
                    else
                    {
                        _filmStripImages = imageFiles ?? Array.Empty<string>();
                        NonstandardFilmstrip = false;
                    }

                    _ = _filmStripWindow.PopulateAsync(_filmStripImages, currentImageIndex, _preloadManager, IsFileBookmarked);

                    // If using retained contents, mark the checkbox as checked immediately
                    if (isUsingRetainedContents)
                    {
                        if (filmStripControl == null)
                        {
                            filmStripControl = FilmStripControl.GetControlFromWindow(_filmStripWindow);
                        }
                        if (filmStripControl != null)
                        {
                            filmStripControl.MarkAsDisplayingRetainedContents();
                        }
                    }
                }
            }
        }

        public void OpenFilmStrip()
        {
            if (_filmStripWindow == null || !_filmStripWindow.IsVisible)
                ToggleFilmStrip(skipPopulation: true);
        }

        private bool TryResolveExplicitDirectoryPath(string inputPath, string targetName, out string directoryPath)
        {
            string targetPath = inputPath.Trim();
            if (targetPath.Length >= 2 && targetPath[0] == '"' && targetPath[^1] == '"')
                targetPath = targetPath[1..^1];
            targetPath = Path.IsPathRooted(targetPath)
                ? Path.GetFullPath(targetPath)
                : Path.GetFullPath(targetPath, currentDirectory ?? Environment.CurrentDirectory);

            if (Directory.Exists(targetPath))
            {
                directoryPath = targetPath;
                return true;
            }

            if (File.Exists(targetPath))
            {
                directoryPath = Path.GetDirectoryName(targetPath)!;
                return true;
            }

            directoryPath = string.Empty;
            Message($"{targetName} path not found: {targetPath}");
            return false;
        }

        private string[] GetSupportedMediaFilesInDirectory(string directoryPath) =>
            Directory.GetFiles(directoryPath).Where(IsSupportedImageFile).ToArray();

        // use when populating film strip in a way other than image's directory's files.
        public void NonstandardPopulateFilmStrip(string[] files)
        {
            NonstandardFilmstrip = true;
            _filmStripImages = files;
            // Any nonstandard (explicit) population overrides retained contents in this window
            ClearFilmstripRetainCheckbox();
            _ = _filmStripWindow.PopulateAsync(_filmStripImages, currentImageIndex, _preloadManager, IsFileBookmarked);
        }

        internal Task SendSelectedMediaToFilmStripAsync(IReadOnlyList<string> filePaths)
        {
            string[] selectedFiles = filePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (selectedFiles.Length == 0)
                return Task.CompletedTask;

            OpenFilmStrip();
            NonstandardPopulateFilmStrip(selectedFiles);
            return Task.CompletedTask;
        }

        private async void OnFilmStripThumbnailClicked(string path, bool openInNewWindow)
        {
            if (openInNewWindow)
            {
                await OpenMediaInNewWindowAsync(path);
            }
            else
            {
                if (NonstandardFilmstrip)
                {
                    await LoadImage(path, openedThroughApp: true);
                }
                else
                {
                    int idx = Array.IndexOf(_filmStripImages ?? Array.Empty<string>(), path);
                    if (idx >= 0)
                    {
                        _ = DisplayImage(idx, openedThroughApp: true);
                    }
                }
            }

            if (_filmStripWindow != null && _filmStripWindow.CloseAfterSelect)
            {
                _filmStripWindow.Hide();
            }
        }

        internal async Task OpenSelectedFilesAsync(IReadOnlyList<string> filePaths)
        {
            string[] selectedFiles = filePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (selectedFiles.Length == 0)
                return;

            await OpenRecentFile(selectedFiles[0]);
            foreach (string filePath in selectedFiles.Skip(1))
                await OpenMediaInNewWindowAsync(filePath);
        }

        internal Task OpenSelectedMediaInNewWindowsAsync(IReadOnlyList<string> filePaths)
        {
            return OpenSelectedMediaInNewWindowsAsync(filePaths, targetPageIndex: null);
        }

        internal async Task OpenSelectedMediaOnPageAsync(IReadOnlyList<string> filePaths, string target)
        {
            int? targetPageIndex;
            if (int.TryParse(target, out int pageIndex))
            {
                if (pageIndex < 1 || pageIndex > 20)
                {
                    Message($"Invalid page index: {pageIndex}. Must be from 1 to 20.");
                    return;
                }

                targetPageIndex = pageIndex;
            }
            else
            {
                targetPageIndex = ResolveSpecialPageToken(target);
                if (!targetPageIndex.HasValue)
                {
                    string targetName = target switch
                    {
                        "pi" => "previous inactive",
                        "pa" => "previous active",
                        "na" => "next active",
                        "ni" => "next inactive",
                        _ => "target"
                    };
                    Message($"There is no {targetName} page available.");
                    return;
                }
            }

            await OpenSelectedMediaInNewWindowsAsync(filePaths, targetPageIndex);
        }

        private async Task OpenSelectedMediaInNewWindowsAsync(IReadOnlyList<string> filePaths, int? targetPageIndex)
        {
            string[] selectedFiles = filePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (string filePath in selectedFiles)
                await OpenMediaInNewWindowAsync(filePath, targetPageIndex);
        }

        internal async Task OpenMediaInNewWindowAsync(string path, int? targetPageIndex = null)
        {
            if (targetPageIndex.HasValue && (targetPageIndex.Value < 1 || targetPageIndex.Value > 20))
            {
                Message($"Invalid page index: {targetPageIndex.Value}. Must be from 1 to 20.");
                return;
            }

            try
            {
                var window = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var newWindow = new MainWindow(path, workspaceLoad: true);
                    newWindow.WorkspaceLoadInProgress = true;
                    return newWindow;
                });

                var loadTask = await window.Dispatcher.InvokeAsync(() => window.LoadImage(path, true));
                await loadTask;
                await window.Dispatcher.InvokeAsync(() => window.Show());
                if (targetPageIndex.HasValue && targetPageIndex.Value != GetCurrentPageIndex())
                    await window.Dispatcher.InvokeAsync(() => window.SendWindowToPage(targetPageIndex.Value));
                await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                if (window.VideoHost.Content is Cloudless.PluginBase.IVideoPlayer)
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    while (stopwatch.ElapsedMilliseconds < 2000)
                    {
                        try
                        {
                            var dimensionsTask = await window.Dispatcher.InvokeAsync(() =>
                                (window.VideoHost.Content as Cloudless.PluginBase.IVideoPlayer)?.GetDimensions());
                            if (dimensionsTask != null)
                            {
                                var dimensions = await dimensionsTask.WaitAsync(TimeSpan.FromMilliseconds(250));
                                if (dimensions != null)
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Video dimension polling error while opening '{path}': {ex}");
                        }

                        await Task.Delay(100);
                    }
                }

                await window.Dispatcher.InvokeAsync(() => window.ResizeWindowToImage(), System.Windows.Threading.DispatcherPriority.Render);
                await window.Dispatcher.InvokeAsync(() => window.CenterWindowOnCurrentScreen(), System.Windows.Threading.DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                Message($"Failed to open media '{path}' in a new window: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the retain checkbox in the currently visible filmstrip only.
        /// Called when an explicit target is applied to clear retain in the receiving window only.
        /// Does not affect other windows or global retain state.
        /// </summary>
        private void ClearFilmstripRetainCheckbox()
        {
            if (_filmStripWindow != null)
            {
                var filmStripControl = FilmStripControl.GetControlFromWindow(_filmStripWindow);
                if (filmStripControl != null)
                {
                    // Use main window dispatcher to ensure proper synchronization
                    this.Dispatcher.Invoke(() =>
                    {
                        filmStripControl.ClearRetainCheckboxOnThisInstanceOnly();
                    }, System.Windows.Threading.DispatcherPriority.Send);
                }
            }
        }
    }
}
