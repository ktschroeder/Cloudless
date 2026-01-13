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
    public class RecentImageItem : INotifyPropertyChanged
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


    public partial class RecentImagesWindow : Window
    {
        public ObservableCollection<RecentImageItem> RecentImages { get; } = new();

        public RecentImagesWindow(IEnumerable<string> recentFiles)
        {
            InitializeComponent();
            DataContext = this;

            foreach (var file in recentFiles)
            {
                RecentImages.Add(new RecentImageItem
                {
                    FilePath = file,
                    Thumbnail = null  // placeholder, pending loading image. Could add an actual placeholder graphic if desired.
                });
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadThumbnailsAsync();
        }

        private async Task LoadThumbnailsAsync()
        {
            foreach (var item in RecentImages)
            {
                if (!File.Exists(item.FilePath))
                    continue;

                ImageSource? thumb = await RunStaAsync(() =>
                    MainWindow.GetImageThumbnail(filePath: item.FilePath, width: 128, height: 128)?.Source);

                if (thumb != null)
                    item.Thumbnail = thumb;
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
            if (sender is FrameworkElement fe &&
                fe.DataContext is RecentImageItem item)
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
