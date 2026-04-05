using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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

            if (workspaceName != null)
            {
                AddWorkspaceButtons();
                PreviewWorkspace = workspaceName;
            } 
        }

        private void AddWorkspaceButtons()
        {
            WorkstationLoadButton.Visibility = Visibility.Visible;
            WorkstationMergeButton.Visibility = Visibility.Visible;
        }

        private async void Workstation_Load_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mw)
            {
                await mw.LoadWorkspace(PreviewWorkspace);
            }
            Close();
        }

        private async void Workstation_Merge_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mw)
            {
                await mw.LoadWorkspace(PreviewWorkspace, merge: true);
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadThumbnailsAsync();
        }

        private async Task LoadThumbnailsAsync()
        {
            foreach (var item in GalleryImages)
            {
                if (!File.Exists(item.FilePath))
                    continue;

                ImageSource? thumb = null;

                if (Owner is MainWindow mw)
                {
                    //thumb = await RunStaAsync(async () =>
                    //    (await mw.GetImageThumbnail(filePath: item.FilePath, width: 128, height: 128))?.Source);

                    thumb = (await mw.GetImageThumbnail(filePath: item.FilePath, width: 128, height: 128))?.Source;
                }

                if (thumb != null)
                    item.Thumbnail = thumb;
            }
        }


        //private static Task<ImageSource> RunStaAsync(Func<Task<ImageSource>> func)  // "Single Thread Apartment". Tragic WPF shenanigans.
        //{
        //    var tcs = new TaskCompletionSource<ImageSource>();

        //    var thread = new Thread(() =>
        //    {
        //        try
        //        {
        //            var result = func();
        //            tcs.SetResult(result);
        //        }
        //        catch (Exception ex)
        //        {
        //            tcs.SetException(ex);
        //        }
        //    });

        //    thread.SetApartmentState(ApartmentState.STA);
        //    thread.IsBackground = true;
        //    thread.Start();

        //    return tcs.Task;
        //}

        private async void Thumbnail_Click(object sender, MouseButtonEventArgs e)
        {
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
