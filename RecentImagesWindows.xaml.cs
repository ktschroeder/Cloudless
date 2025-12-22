using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Path = System.IO.Path;

namespace Cloudless
{
    public class RecentImageItem
    {
        public string FilePath { get; init; }
        public string FileName => Path.GetFileName(FilePath);
        public ImageSource Thumbnail { get; init; }
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
                if (!File.Exists(file)) continue;

                RecentImages.Add(new RecentImageItem
                {
                    FilePath = file,
                    Thumbnail = MainWindow.GetImageThumbnail(file, 128, 128).Source
                });
            }
        }

        private void Thumbnail_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe &&
                fe.DataContext is RecentImageItem item)
            {
                if (Owner is MainWindow mw)
                    mw.OpenRecentFile(item.FilePath);

                Close();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        private void Window_KeyDown(object sender, KeyEventArgs e) { WindowHelper.HandleKeyDown(this, e); }
    }

}
