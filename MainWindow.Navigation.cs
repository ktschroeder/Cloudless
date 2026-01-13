using System.Windows.Controls;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            OpenImage();
        }
        private void MinimizeWindow()
        {
            this.WindowState = WindowState.Minimized;
        }
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            MinimizeWindow();
        }
        private void OpenPreferences_Click(object sender, RoutedEventArgs e)
        {
            OpenPreferences();
        }
        private void OpenPreferences()
        {
            var configWindow = new ConfigurationWindow();

            // Center the window relative to the main application window
            configWindow.Owner = this; // Set the owner to the main window
            configWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (configWindow.ShowDialog() == true)
            {
                // Save the new preference
                Cloudless.Properties.Settings.Default.DisplayMode = configWindow.SelectedDisplayMode;
                Cloudless.Properties.Settings.Default.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle = configWindow.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle;
                Cloudless.Properties.Settings.Default.PixelsSpaceAroundBounds = configWindow.SpaceAroundBounds;
                Cloudless.Properties.Settings.Default.ResizeWindowToNewImageWhenOpeningThroughApp = configWindow.ResizeWindowToNewImageWhenOpeningThroughApp;
                Cloudless.Properties.Settings.Default.BorderOnMainWindow = configWindow.BorderOnMainWindow;
                Cloudless.Properties.Settings.Default.LoopGifs = configWindow.LoopGifs;
                Cloudless.Properties.Settings.Default.MuteMessages = configWindow.MuteMessages;
                Cloudless.Properties.Settings.Default.AlwaysOnTopByDefault = configWindow.AlwaysOnTopByDefault;
                Cloudless.Properties.Settings.Default.MaxCompressedCopySizeMB = configWindow.MaxCompressedCopySizeMB;
                Cloudless.Properties.Settings.Default.Background = configWindow.SelectedBackground;
                var previousSortOrder = Cloudless.Properties.Settings.Default.ImageDirectorySortOrder;
                Cloudless.Properties.Settings.Default.ImageDirectorySortOrder = configWindow.SelectedSortOrder;
                Cloudless.Properties.Settings.Default.DisableSmartZoom = configWindow.DisableSmartZoom;
                Cloudless.Properties.Settings.Default.WebmEnabled = configWindow.WebmEnabled;

                Cloudless.Properties.Settings.Default.Save();

                if (!previousSortOrder.Equals(configWindow.SelectedSortOrder))
                {
                    SortImageFilesArray();
                }

                SetBackground();

                ApplyDisplayMode();
            }
        }
        private void OpenRecentImagesWindow()
        {
            var win = new RecentImagesWindow(recentFiles);
            win.Owner = this;
            win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            win.Show();
        }
        public void SetBackground()
        {
            string selectedBackground = Properties.Settings.Default.Background;
            if (selectedBackground == "white")
            {
                this.Background = new SolidColorBrush(new System.Windows.Media.Color() { ScR = 1, ScG = 1, ScB = 1, ScA = 1 });
            }
            else if (selectedBackground == "transparent")
            {
                this.Background = new SolidColorBrush(new System.Windows.Media.Color() { ScA = 0 });
            }
            else  // "black"
            {
                this.Background = new SolidColorBrush(new System.Windows.Media.Color() { ScA = 1 });
            }
        }

        private void SetDimensions_Click(object sender, RoutedEventArgs e)
        {
            SetDimensions();
        }
        private void SetDimensions()
        {
            var setDimensionsWindow = new SetDimensionsWindow(this.Width, this.Height);

            // Center the window relative to the main application window
            setDimensionsWindow.Owner = this; // Set the owner to the main window
            setDimensionsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (setDimensionsWindow.ShowDialog() == true)
            {
                ResizeWindow(setDimensionsWindow.NewWidth, setDimensionsWindow.NewHeight);
                CenterWindow();
            }
        }
        private void DuplicateWindow_Click(object sender, RoutedEventArgs e)
        {
            DuplicateWindow();
        }
        private void DuplicateWindow()
        {
            //debugger shows binding errors but may not be real issue, see perhaps https://stackoverflow.com/questions/14526371/menuitem-added-programmatically-causes-binding-error
            var duplicateWindow = new MainWindow(currentlyDisplayedImagePath, this.Width, this.Height);
            // could also include viewing mode, possibly panning/zooming etc. But this can get messy and imperfect unless total state is matched properly (preferences may not be aligned)

            //setDimensionsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            duplicateWindow.Show();
        }
        private void About_Click(object sender, RoutedEventArgs e)
        {
            About();
        }
        private void About()
        {
            var aboutWindow = new AboutWindow(CURRENT_VERION);

            // Center the window relative to the main application window
            aboutWindow.Owner = this; // Set the owner to the main window
            aboutWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            aboutWindow.Show();
        }
        private void HotkeyRef_Click(object sender, RoutedEventArgs e)
        {
            HotkeyRef();
        }
        private void HotkeyRef()
        {
            var hkrWindow = new HotkeyRefWindow();

            // Center the window relative to the main application window
            hkrWindow.Owner = this; // Set the owner to the main window
            // could make this not stay above main window, by not setting the owner.
            hkrWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            hkrWindow.Show();
        }
        private void CommandPaletteRef_Click(object sender, RoutedEventArgs e)
        {
            CommandPaletteRef();
        }
        private void CommandPaletteRef()
        {
            var cprWindow = new CommandRefWindow();

            // Center the window relative to the main application window
            cprWindow.Owner = this; // Set the owner to the main window
            // could make this not stay above main window, by not setting the owner.
            cprWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            cprWindow.Show();
        }
        private void ToggleFullscreen()
        {
            ToggleCropMode(false);
            if (WindowState == WindowState.Normal)
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Normal;
            }
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void ImageInfo_Click(object sender, RoutedEventArgs e)
        {
            ImageInfo();
        }
        private void ImageInfo()
        {
            if (string.IsNullOrEmpty(currentlyDisplayedImagePath))
                return;

            var imageInfoWindow = new ImageInfoWindow(currentlyDisplayedImagePath)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            imageInfoWindow.ShowDialog();
        }
        private void UpdateContextMenuState()
        {
            // Enable/disable "Image Info" based on loaded image
            var imageInfoMenuItem = ImageContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header.ToString() == "Image Info");
            if (imageInfoMenuItem != null)
                imageInfoMenuItem.IsEnabled = !string.IsNullOrEmpty(currentlyDisplayedImagePath);

            var zoomMenuItem = ImageContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header.ToString().StartsWith("Zoom"));
            var scaleX = imageScaleTransform?.ScaleX;
            var scaleY = imageScaleTransform?.ScaleY; // TODO do math to show user a user-friendly effective zoom level
            if (zoomMenuItem != null)
            {
                zoomMenuItem.Header = "Zoom";  // TODO maybe disable here, e.g. cannot use in zen mode

                if (ImageDisplay != null && ImageDisplay.Source is BitmapSource bitmap && scaleX != null && scaleY != null)
                {
                    double imageWidth = ImageDisplay.ActualWidth;
                    double imageTrueWidth = bitmap.PixelWidth;
                    var realScale = imageWidth / (double)imageTrueWidth * (double)scaleX;  // ignores nuance if x and y scales don't match, i.e. stretching

                    zoomMenuItem.Header = $"Zoom ({(int)double.Round(realScale * 100)}%)";
                }
            }
        }
        private void OpenMessageHistory_Click(object sender, RoutedEventArgs e)
        {
            var historyWindow = new MessageHistoryWindow(overlayManager);
            historyWindow.Owner = this; // Set the owner to the main window
            historyWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            historyWindow.Show();
        }

        private void ShowContextMenu()
        {
            ContextMenu menu = ImageContextMenu;
            // menu.PlacementTarget = target; // UIElement
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
        }
    }
}
