using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Linq;
using Path = System.IO.Path;

namespace Cloudless
{
    public class GalleryItem : INotifyPropertyChanged
    {
        public string? FilePath { get; init; }
        public string? FileName => Path.GetFileName(FilePath);

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
        public ObservableCollection<GalleryItem> GalleryImages { get; } = new();
        public string? PreviewWorkspace;

        public GalleryWindow(IEnumerable<string> galleryFiles, string title = "Image Gallery", string? workspaceName = null)
        {
            InitializeComponent();
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

        private async void Workspace_Merge_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mw)
            {
                await mw.LoadWorkspace(PreviewWorkspace, merge: true);
            }
        }

        private bool OpenFileDialogIsOpen = false;  // Without managing anything, WPF weirdly receives a click event on an image thumb when double clicking a file in the OpenFileDialog. This guards against that.
        private async void Workspace_Preview_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mw)
            {
                OpenFileDialogIsOpen = true;
                string? workspaceName = await mw.SelectWorkspaceFileToPreview();
                OpenFileDialogIsOpen = false;
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
            await LoadThumbnailsAsync();
        }

        private async Task LoadThumbnailsAsync()
        {
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
                    try { (Owner as MainWindow)?.Message("One or more thumbnails failed to load in gallery."); } catch { }
                }
            });
        }

        private async Task LoadAndSetThumbnailAsync(GalleryItem item, int width, int height)
        {
            ImageSource? thumb = null;
            try
            {
                string path = item.FilePath ?? "";
                string ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
                bool isVideo = ext == ".webm" || ext == ".mkv" || ext == ".mp4" || ext == ".avi" || ext == ".mov";

                if (isVideo)
                {
                    // ThumbnailService returns a frozen BitmapSource and does IO off-thread
                    thumb = await ThumbnailService.GetThumbnailAsync(path, width, height);
                }
                else
                {
                    // Load image on a background thread and freeze it so it can be assigned from UI thread
                    thumb = await Task.Run(() =>
                    {
                        try
                        {
                            using var fs = File.OpenRead(path);
                            var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                            var frame = decoder.Frames.FirstOrDefault();
                            if (frame != null)
                            {
                                frame.Freeze();
                                return (ImageSource)frame;
                            }
                        }
                        catch { }
                        return (ImageSource?)null;
                    });
                }
            }
            catch (Exception ex)
            {
                try { (Owner as MainWindow)?.Message($"Error loading thumbnail for {item.FilePath}: {ex.Message}"); } catch { }
            }

            if (thumb == null)
            {
                string failPath = Path.Combine(AppContext.BaseDirectory, "no-thumbnail.png");
                if (File.Exists(failPath))
                {
                    try
                    {
                        var bi = new BitmapImage(new Uri(failPath));
                        bi.Freeze();
                        thumb = bi;
                    }
                    catch { }
                }
            }

            if (thumb != null)
            {
                try
                {
                    await Dispatcher.InvokeAsync(() => item.Thumbnail = thumb);
                }
                catch { }
            }
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
            if (OpenFileDialogIsOpen)
                return;

            if (sender is FrameworkElement fe &&
                fe.DataContext is GalleryItem item)
            {
                if (Owner is MainWindow mw)
                    await mw.OpenRecentFile(item.FilePath ?? "");

                Close();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        private void Window_KeyDown(object sender, KeyEventArgs e) { WindowHelper.HandleKeyDown(this, e); }
        private void Close_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }
    }

}
