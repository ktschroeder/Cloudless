using System.Windows.Controls;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Threading.Tasks;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        private async void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            await OpenImage();
        }
        private void MinimizeWindow(CloudlessWindowState stateFromWorkspaceLoad = null)
        {
            if (stateFromWorkspaceLoad != null)
            {
                stateUponMinimizing = stateFromWorkspaceLoad;
                stateUponMinimizing.IsMinimized = false;
            }
            else
                stateUponMinimizing = GetWindowState(GetZOrderForCurrentProcessWindows());
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
                Cloudless.Properties.Settings.Default.ImgBBKey = configWindow.ImgBBKey;

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
            var win = new GalleryWindow(recentFiles, title: "Recent Images Gallery");
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
                CenterWindowOnCurrentScreen();
            }
        }
        private async void DuplicateWindow_Click(object sender, RoutedEventArgs e)
        {
            await DuplicateWindow();
        }
        private async Task DuplicateWindow()
        {
            var state = GetWindowState(GetZOrderForCurrentProcessWindows());
            state.Left += 20;  // make new window appear a bit down and right to make it more clear there is a new window, similar to typical Windows behavior
            state.Top += 20;

            var duplicateWindow = new MainWindow("");
            if (!string.IsNullOrEmpty(currentlyDisplayedImagePath))
            {
                await duplicateWindow.LoadImage(currentlyDisplayedImagePath, false);
            }

            duplicateWindow.ApplyWindowState(state);
            duplicateWindow.Show();
            duplicateWindow.PostProcessLoadedWindow(state);
            duplicateWindow.Activate();
        }
        private void About_Click(object sender, RoutedEventArgs e)
        {
            About();
        }
        private void About()
        {
            var aboutWindow = new AboutWindow(CURRENT_VERSION);

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
        private CommandRefWindow CommandPaletteRef()
        {
            var cprWindow = new CommandRefWindow();

            // Center the window relative to the main application window
            cprWindow.Owner = this; // Set the owner to the main window
            // could make this not stay above main window, by not setting the owner.
            cprWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            cprWindow.Show();
            return cprWindow;
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
        private PopOutCommandPaletteWindow OpenPopOutCommandPaletteWindow()
        {
            var pocpWindow = new PopOutCommandPaletteWindow();

            pocpWindow.Owner = this;
            pocpWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            pocpWindow.Show();
            pocpWindow.CommandTextBox.Focus();
            return pocpWindow;
        }
        private async Task UpdateContextMenuState()
        {
            // Enable/disable "Image Info" based on loaded image
            var imageInfoMenuItem = ImageContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header.ToString() == "Image Info");
            if (imageInfoMenuItem != null)
                imageInfoMenuItem.IsEnabled = !string.IsNullOrEmpty(currentlyDisplayedImagePath);

            var zoomMenuItem = ImageContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header.ToString().StartsWith("Zoom"));
            var scaleX = imageScaleTransform?.ScaleX;
            var scaleY = imageScaleTransform?.ScaleY;
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

            await UpdateRecentFilesMenu();
        }
        private void OpenMessageHistory_Click(object sender, RoutedEventArgs e)
        {
            OpenMessageHistory();
        }

        private void OpenMessageHistory()
        {
            var historyWindow = new MessageHistoryWindow(overlayManager);
            historyWindow.Owner = this;
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

        private async Task GoToPreviousImage()
        {
            if (imageFiles == null) return;
            // Go to the previous image
            currentImageIndex = (currentImageIndex == 0) ? imageFiles.Length - 1 : currentImageIndex - 1;
            await DisplayImage(currentImageIndex, true);
        }

        private async Task GoToNextImage()
        {
            if (imageFiles == null) return;
            // Go to the next image
            currentImageIndex = (currentImageIndex == imageFiles.Length - 1) ? 0 : currentImageIndex + 1;
            await DisplayImage(currentImageIndex, true);
        }
    }
}
