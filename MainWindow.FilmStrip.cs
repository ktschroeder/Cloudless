using System;
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
                _ = _filmStripWindow.PopulateAsync(files, currentImageIndex, _preloadManager);
            }
        }

        public void ToggleFilmStrip(bool skipPopulation = false)
        {
            if (_filmStripWindow == null)
            {
                _filmStripWindow = new FilmStripWindow();
                _filmStripWindow.ThumbnailClicked += OnFilmStripThumbnailClicked;
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

                    _ = _filmStripWindow.PopulateAsync(_filmStripImages, currentImageIndex, _preloadManager);

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

        // use when populating film strip in a way other than image's directory's files.
        public void NonstandardPopulateFilmStrip(string[] files)
        {
            NonstandardFilmstrip = true;
            _filmStripImages = files;
            // Any nonstandard (explicit) population overrides retained contents in this window
            ClearFilmstripRetainCheckbox();
            _ = _filmStripWindow.PopulateAsync(_filmStripImages, currentImageIndex, _preloadManager);
        }

        private async void OnFilmStripThumbnailClicked(string path, bool openInNewWindow)
        {
            if (openInNewWindow)
            {
                var w = new MainWindow(path);
                w.Show();
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
