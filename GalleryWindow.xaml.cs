using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace Cloudless
{
    public class GalleryItem : INotifyPropertyChanged
    {
        public string? FilePath { get; init; }
        public string? FileName => Path.GetFileName(FilePath);
        public string TooltipFileName => FilmStripControl.GetTooltipFileName(FilePath ?? string.Empty);
        public bool IsVlcVideo => VideoThumbnailOverlay.IsVlcVideoPath(FilePath);

        private bool _isAnimated;
        public bool IsAnimated
        {
            get => _isAnimated;
            set
            {
                if (_isAnimated != value)
                {
                    _isAnimated = value;
                    PropertyChanged?.Invoke(this, new(nameof(IsAnimated)));
                }
            }
        }

        private bool _isBookmarked;
        public bool IsBookmarked
        {
            get => _isBookmarked;
            set
            {
                if (_isBookmarked != value)
                {
                    _isBookmarked = value;
                    PropertyChanged?.Invoke(this, new(nameof(IsBookmarked)));
                }
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new(nameof(IsSelected)));
                }
            }
        }

        private ImageSource? _thumbnail;
        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (_thumbnail != value)
                {
                    _thumbnail = value;
                    PropertyChanged?.Invoke(this, new(nameof(Thumbnail)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }


    public partial class GalleryWindow : Window
    {
        private readonly SemaphoreSlim _thumbnailLoadSemaphore = new(2);
        private int _selectionAnchorIndex = -1;
        private int _keyboardFocusIndex = -1;

        public ObservableCollection<GalleryItem> GalleryImages { get; } = new();
        public string? PreviewWorkspace;

        public GalleryWindow(IEnumerable<string> galleryFiles, string title = "Image Gallery", string? workspaceName = null)
        {
            InitializeComponent();
            SecondaryWindowOpacity.Apply(this);
            DataContext = this;

            Title = title;
            TitleText.Text = title;

            foreach (var file in galleryFiles)
            {
                GalleryImages.Add(new GalleryItem
                {
                    FilePath = file,
                    Thumbnail = null  // placeholder, pending loading image. Could add an actual placeholder graphic if desired.
                });
            }

            AddWorkspaceButtons(!string.IsNullOrEmpty(workspaceName));
            PreviewWorkspace = workspaceName;
            UpdateSelectionUi();
        }

        private void AddWorkspaceButtons(bool wsSelected)
        {
            if (wsSelected) 
            {
                WorkspaceLoadButton.Visibility = Visibility.Visible;
                WorkspaceMergeButton.Visibility = Visibility.Visible;
                WorkspacePreviewButton.Content = "Preview Another WS";
                WorkspacePreviewButton.Visibility = Visibility.Visible;
            }
            
            if (!wsSelected)
            {
                WorkspacePreviewButton.Content = "Preview a WS";
                WorkspacePreviewButton.Visibility = Visibility.Visible;
            }
        }

        private async void Workspace_Load_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mw)
            {
                await mw.LoadWorkspace(PreviewWorkspace);
            }
            Close();
        }

        private async void FilmstripButton_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mw)
            {
                mw.OpenFilmStrip();
                mw.NonstandardPopulateFilmStrip(GalleryImages.Select(i => i.FilePath)?.ToArray() ?? Array.Empty<string>());
            }
            Close();
        }

        private async void Workspace_Merge_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mw)
            {
                await mw.LoadWorkspace(PreviewWorkspace, merge: true);
            }
        }

        private bool OpenFileDialogIsOpen = false;  // Without managing anything, WPF weirdly receives a click event on an image thumb when double clicking a file in the OpenFileDialog. This guards against that.
        private DispatcherTimer _workspacePreviewClicked;
        private async void Workspace_Preview_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mw)
            {
                OpenFileDialogIsOpen = true;
                string? workspaceName = await mw.SelectWorkspaceFileToPreview();

                OpenFileDialogIsOpen = false;
                // This timer is a workaround for the WPF bug where WPF can receive a click event on an image thumbnail when double-clicking a file in the Windows OpenFileDialog. The timer ensures that the click event is ignored for a short period after the OpenFileDialog is closed.
                _workspacePreviewClicked = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
                _workspacePreviewClicked.Tick += (s, args) =>
                {
                    _workspacePreviewClicked?.Stop();
                };
                _workspacePreviewClicked.Start();

                if (string.IsNullOrEmpty(workspaceName))
                    return;


                // We could simply create a new gallery and close this one, but it's cleaner, to repopulate images.
                string workspaceFilePath = Path.Combine(MainWindow.workspaceFilesPath, workspaceName + ".cloudless");
                string json = File.ReadAllText(workspaceFilePath);
                var workspace = JsonSerializer.Deserialize<CloudlessWorkspace>(json);

                if (workspace == null)
                {
                    mw.Message("Invalid workspace file.");
                    return;
                }

                var wsFiles = workspace.CloudlessWindows.Select(cw => cw.ImagePath);

                string title = "Workspace Preview: " + workspaceName;
                Title = title;
                TitleText.Text = title;

                GalleryImages.Clear();
                _selectionAnchorIndex = -1;
                _keyboardFocusIndex = -1;
                UpdateSelectionUi();

                foreach (var file in wsFiles)
                {
                    GalleryImages.Add(new GalleryItem
                    {
                        FilePath = file,
                        Thumbnail = null  // placeholder, pending loading image. Could add an actual placeholder graphic if desired.
                    });
                }

                PreviewWorkspace = workspaceName;
                AddWorkspaceButtons(!string.IsNullOrEmpty(workspaceName));
                await LoadThumbnailsAsync();  // See OpenFileDialogIsOpen definition comment.

            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshBookmarkIndicators();
            await LoadThumbnailsAsync();
        }

        internal void RefreshBookmarkIndicators(Func<string?, bool>? isBookmarked = null)
        {
            if (isBookmarked == null)
                isBookmarked = path => Owner is MainWindow mw && mw.IsFileBookmarked(path);
            foreach (var item in GalleryImages)
                item.IsBookmarked = isBookmarked(item.FilePath);
        }

        private async Task LoadThumbnailsAsync()
        {
            RefreshBookmarkIndicators();
            var tasks = new List<Task>();
            foreach (var item in GalleryImages)
            {
                if (!File.Exists(item.FilePath))
                    continue;

                // Fire-and-forget per-item thumbnail load so the UI stays responsive and thumbnails appear as they arrive.
                tasks.Add(LoadAndSetThumbnailAsync(item, 128, 128));
            }

            // Don't await all to keep UI responsive; but ensure background exceptions are observed
            _ = Task.WhenAll(tasks).ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    (Owner as MainWindow)?.Message("One or more thumbnails failed to load in gallery.");
                }
            });
        }

        private async Task LoadAndSetThumbnailAsync(GalleryItem item, int width, int height)
        {
            ImageSource? thumb = null;
            Task<bool>? animationTask = null;
            try
            {
                string path = item.FilePath ?? "";
                string ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
                bool isVideo = ext == ".webm" || ext == ".mkv" || ext == ".mp4" || ext == ".avi" || ext == ".mov";
                if (AnimatedImageDetector.IsSupportedAnimatedImagePath(path))
                    animationTask = AnimatedImageDetector.IsAnimatedAsync(path);

                await _thumbnailLoadSemaphore.WaitAsync();
                try
                {
                    if (isVideo)
                    {
                        // ThumbnailService returns a frozen BitmapSource and does IO off-thread
                        thumb = await ThumbnailService.GetThumbnailAsync(path, width, height);
                    }
                    else
                    {
                        thumb = await Task.Run(() => LoadDownsampledImageThumbnail(path, width, height));
                    }
                }
                finally
                {
                    _thumbnailLoadSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                (Owner as MainWindow)?.Message($"Error loading thumbnail for {item.FilePath}: {ex.Message}");
            }

            if (thumb == null)
            {
                string failPath = Path.Combine(AppContext.BaseDirectory, "no-thumbnail.png");
                if (File.Exists(failPath))
                {
                    var bi = new BitmapImage(new Uri(failPath));
                    bi.Freeze();
                    thumb = bi;
                }
            }

            if (thumb != null)
            {
                await Dispatcher.InvokeAsync(() => item.Thumbnail = thumb);
            }

            if (animationTask != null)
            {
                bool isAnimated = await animationTask;
                await Dispatcher.InvokeAsync(() => item.IsAnimated = isAnimated);
            }
        }

        private static ImageSource? LoadDownsampledImageThumbnail(string path, int maxWidth, int maxHeight)
        {
            int sourceWidth;
            int sourceHeight;

            using (var stream = File.OpenRead(path))
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                var frame = decoder.Frames.FirstOrDefault();
                if (frame == null)
                    return null;

                sourceWidth = frame.PixelWidth;
                sourceHeight = frame.PixelHeight;
            }

            if (sourceWidth <= 0 || sourceHeight <= 0)
                return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

            double sourceAspectRatio = (double)sourceWidth / sourceHeight;
            double targetAspectRatio = (double)maxWidth / maxHeight;
            if (sourceAspectRatio >= targetAspectRatio)
                bitmap.DecodePixelWidth = maxWidth;
            else
                bitmap.DecodePixelHeight = maxHeight;

            bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }


        private static Task<ImageSource> RunStaAsync(Func<ImageSource> func)  // "Single Thread Apartment". Tragic WPF shenanigans.
        {
            var tcs = new TaskCompletionSource<ImageSource>();

            var thread = new Thread(() =>
            {
                try
                {
                    var result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            return tcs.Task;
        }

        private async void Thumbnail_Click(object sender, MouseButtonEventArgs e)
        {
            bool timerStillLive = _workspacePreviewClicked?.IsEnabled ?? false;
            if (OpenFileDialogIsOpen || timerStillLive)
                return;

            if (sender is FrameworkElement fe &&
                fe.DataContext is GalleryItem item)
            {
                if (e.ClickCount >= 2)
                {
                    await OpenSelectedItemsAsync(item);
                    e.Handled = true;
                    return;
                }

                SelectGalleryItem(item, Keyboard.Modifiers);
                e.Handled = true;
            }
        }

        private void SelectGalleryItem(GalleryItem item, ModifierKeys modifiers)
        {
            int index = GalleryImages.IndexOf(item);
            if (index < 0)
                return;

            _keyboardFocusIndex = index;
            bool control = modifiers.HasFlag(ModifierKeys.Control);
            bool shift = modifiers.HasFlag(ModifierKeys.Shift);
            if (shift && _selectionAnchorIndex >= 0)
            {
                if (!control)
                    foreach (var galleryItem in GalleryImages)
                        galleryItem.IsSelected = false;

                int start = Math.Min(_selectionAnchorIndex, index);
                int end = Math.Max(_selectionAnchorIndex, index);
                for (int i = start; i <= end; i++)
                    GalleryImages[i].IsSelected = true;
            }
            else if (control)
            {
                item.IsSelected = !item.IsSelected;
                _selectionAnchorIndex = index;
            }
            else
            {
                foreach (var galleryItem in GalleryImages)
                    galleryItem.IsSelected = false;
                item.IsSelected = true;
                _selectionAnchorIndex = index;
            }

            UpdateSelectionUi();
        }

        private void UpdateSelectionUi()
        {
            int selectedCount = GalleryImages.Count(item => item.IsSelected);
            SelectionActionsPanel.Visibility = Visibility.Visible;
            SelectionCountText.Text = $"{selectedCount} selected";
            EditSelectedTagsButton.IsEnabled = selectedCount > 0;
            BookmarkSelectedButton.IsEnabled = selectedCount > 0;
            RemoveBookmarksButton.IsEnabled = selectedCount > 0;
            ClearSelectionButton.IsEnabled = selectedCount > 0;
        }

        private void Thumbnail_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not GalleryItem item)
                return;

            if (!item.IsSelected)
            {
                foreach (var galleryItem in GalleryImages)
                    galleryItem.IsSelected = false;
                item.IsSelected = true;
                _selectionAnchorIndex = GalleryImages.IndexOf(item);
                _keyboardFocusIndex = _selectionAnchorIndex;
            }
            UpdateSelectionUi();
        }

        private void Thumbnail_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
                element.ContextMenu = BulkTagContextMenu.Create(
                    EditTagsForSelection,
                    () => EditBookmarksForSelection(add: true),
                    () => EditBookmarksForSelection(add: false));
        }

        private void EditSelectedTags_Click(object sender, RoutedEventArgs e) => EditTagsForSelection();

        private void EditTagsForSelection()
        {
            if (Owner is MainWindow mw)
            {
                string[] selectedFiles = GalleryImages
                    .Where(item => item.IsSelected && !string.IsNullOrWhiteSpace(item.FilePath))
                    .Select(item => item.FilePath!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                mw.EditTagsForFiles(selectedFiles);
            }
        }

        private void BookmarkSelected_Click(object sender, RoutedEventArgs e) => EditBookmarksForSelection(add: true);

        private void RemoveBookmarks_Click(object sender, RoutedEventArgs e) => EditBookmarksForSelection(add: false);

        private void EditBookmarksForSelection(bool add)
        {
            if (Owner is MainWindow mw)
            {
                string[] selectedFiles = GalleryImages
                    .Where(item => item.IsSelected && !string.IsNullOrWhiteSpace(item.FilePath))
                    .Select(item => item.FilePath!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                mw.EditBookmarksForFiles(selectedFiles, add);
            }
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in GalleryImages)
                item.IsSelected = false;
            _selectionAnchorIndex = -1;
            _keyboardFocusIndex = -1;
            UpdateSelectionUi();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            SelectAllItems();
        }

        private void SelectAllItems()
        {
            foreach (var item in GalleryImages)
                item.IsSelected = true;
            if (GalleryImages.Count > 0)
                _selectionAnchorIndex = 0;
            _keyboardFocusIndex = GalleryImages.Count > 0 ? 0 : -1;
            UpdateSelectionUi();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
            {
                MoveGallerySelection(e.Key, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                await OpenSelectedItemsAsync();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                SelectAllItems();
                e.Handled = true;
                return;
            }

            WindowHelper.HandleKeyDown(this, e);
        }

        private void MoveGallerySelection(Key key, bool extendSelection)
        {
            if (GalleryImages.Count == 0)
                return;

            int currentIndex = _keyboardFocusIndex;
            if (currentIndex < 0 || currentIndex >= GalleryImages.Count)
            {
                currentIndex = GalleryImages
                    .Select((item, index) => (item, index))
                    .FirstOrDefault(pair => pair.item.IsSelected, (null!, -1)).index;
            }

            if (currentIndex < 0)
            {
                currentIndex = key is Key.Left or Key.Up ? GalleryImages.Count - 1 : 0;
                _selectionAnchorIndex = currentIndex;
                _keyboardFocusIndex = currentIndex;
                GalleryImages[currentIndex].IsSelected = true;
                UpdateSelectionUi();
                BringGalleryItemIntoView(currentIndex);
                return;
            }

            int columns = Math.Max(1, (int)Math.Floor((GalleryScrollViewer.ViewportWidth > 0
                ? GalleryScrollViewer.ViewportWidth
                : ActualWidth - 24) / 160));
            int targetIndex = key switch
            {
                Key.Left => Math.Max(0, currentIndex - 1),
                Key.Right => Math.Min(GalleryImages.Count - 1, currentIndex + 1),
                Key.Up => currentIndex < columns ? currentIndex : currentIndex - columns,
                Key.Down => Math.Min(GalleryImages.Count - 1, currentIndex + columns),
                _ => currentIndex
            };

            if (targetIndex == currentIndex)
                return;

            if (extendSelection)
            {
                if (_selectionAnchorIndex < 0)
                    _selectionAnchorIndex = currentIndex;
                foreach (var item in GalleryImages)
                    item.IsSelected = false;
                for (int index = Math.Min(_selectionAnchorIndex, targetIndex);
                     index <= Math.Max(_selectionAnchorIndex, targetIndex);
                     index++)
                    GalleryImages[index].IsSelected = true;
            }
            else
            {
                foreach (var item in GalleryImages)
                    item.IsSelected = false;
                GalleryImages[targetIndex].IsSelected = true;
                _selectionAnchorIndex = targetIndex;
            }

            _keyboardFocusIndex = targetIndex;
            UpdateSelectionUi();
            BringGalleryItemIntoView(targetIndex);
        }

        private void BringGalleryItemIntoView(int index)
        {
            if (GalleryItemsControl.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement container)
                container.BringIntoView();
        }

        private async Task OpenSelectedItemsAsync(GalleryItem? doubleClickedItem = null)
        {
            if (Owner is not MainWindow mainWindow)
                return;

            string[] selectedFiles = GalleryImages
                .Where(item => item.IsSelected && !string.IsNullOrWhiteSpace(item.FilePath))
                .Select(item => item.FilePath!)
                .ToArray();

            if (doubleClickedItem != null && !string.IsNullOrWhiteSpace(doubleClickedItem.FilePath))
                selectedFiles = new[] { doubleClickedItem.FilePath };

            if (selectedFiles.Length == 0)
                return;

            Close();
            await mainWindow.OpenSelectedFilesAsync(selectedFiles);
        }
        private void Close_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }
    }

}
