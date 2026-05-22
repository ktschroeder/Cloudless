//using WpfAnimatedGif;
using AnimatedImage.Wpf;
using Cloudless.Properties;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Point = System.Windows.Point;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        private bool isDraggingWindow = false;
        private Point initialMouseScreenPosition;
        private Point initialWindowPosition;

        //Window snapping preference:
        // TODO make this a configurable setting (an advanced setting)
        //- option 1 (fluid & snapless): Native Windows snapping is disabled. When starting to drag window, regardless of whether you are holding SHIFT, you may press/release SHIFT at-will during the drag and the window will intuitively switch between constrained drag and free drag modes.
        //- option 2 (default): When you start dragging, you will enter one of two paths during the drag: If you were not initially holding Shift, native Windows snapping will be enabled, but you cannot enter constrained drag mode by then pressing SHIFT. If you were initially holding Shift, you will see behavior as described in option 1 above.
        private bool fluidSnaplessDragPreference = false;
        // It isn't feasible to dynamically switch between DragMove (needed for Windows's native snapping) and constrained movement in the same drag. DragMove prevents hitting Window_MouseMove.

        // MouseDown: Start Dragging
        public Point TargetCenterForQuickCommandDisplay;
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
            {
                Point currentMousePosition = e.GetPosition(this);

                TargetCenterForQuickCommandDisplay = currentMousePosition;

                MiddleClickDown();
                return;
            }

            CloseQuickCommandWindow();

            if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control || MouseControlMode)
                {
                    if (!isExplorationMode) EnterExplorationMode();

                    if (!MouseControlMode && !MouseCommandMode)
                        this.Cursor = Cursors.SizeAll;
                    isPanningImage = true;
                    lastMousePosition = e.GetPosition(this);
                    ImageDisplay.CaptureMouse(); // bookmark line. captured mouse position could be different than expected due to subsequent automatic panning such as to center/bound image?
                }
                else if (WindowState == WindowState.Maximized)
                {
                    // TODO avoided some grief by just disabling this functionality until it works fully.
                    // Frankly not needed functionality anyway
                    //isDraggingWindowFromFullscreen = true;

                    // TODO weirdness here that is new? or maybe not.
                    // may help: when going fullscreen and dragging out of it, set tracked "window position" to middle of screen instead of real last position that it would otherwise return to.
                }
                else if (WindowState == WindowState.Normal)
                {
                    isDraggingWindow = true;
                    initialMouseScreenPosition = PointToScreen(e.GetPosition(this)); // Use screen coordinates
                    initialWindowPosition = new Point(this.Left, this.Top);

                    if (fluidSnaplessDragPreference || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        Mouse.Capture(this);  // Capture mouse for consistent dragging
                    else
                        DragMove();
                }
            }
        }

        // MouseMove: Handle Dragging with Axis Constraining
        private async void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanningImage)
            {
                if (!isExplorationMode) EnterExplorationMode();

                Point currentMousePosition = e.GetPosition(this);
                Vector delta = currentMousePosition - lastMousePosition;
                ClampTransformToIntuitiveBounds(delta);
                lastMousePosition = currentMousePosition;
            }
            else if (isDraggingWindowFromFullscreen && WindowState == WindowState.Maximized)
            {
                // Handle dragging window out of fullscreen
                Point cursorPosition = e.GetPosition(this);
                await ToggleFullscreen();
                isDraggingWindow = true;  // this will then hit the below if-block

                // TODO center window on cursor

                //DragMove();  // TODO exception here if you click and drag from fullscreen, and without releasing, drag to top of display to again enter fullscreen, then release.
            }

            if (isDraggingWindow && e.LeftButton == MouseButtonState.Pressed)
            {
                Mouse.Capture(this);  // Capture mouse for consistent dragging

                Point currentMouseScreenPosition = PointToScreen(e.GetPosition(this)); // Screen coordinates
                Vector mouseDelta = currentMouseScreenPosition - initialMouseScreenPosition;

                double newLeft = initialWindowPosition.X + mouseDelta.X;
                double newTop = initialWindowPosition.Y + mouseDelta.Y;

                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    if (Math.Abs(mouseDelta.X) > Math.Abs(mouseDelta.Y))
                    {
                        // Horizontal movement only
                        RepositionWindow(newLeft, initialWindowPosition.Y);
                    }
                    else
                    {
                        // Vertical movement only
                        RepositionWindow(initialWindowPosition.X, newTop);
                    }
                }
                else
                {
                    RepositionWindow(newLeft, newTop);
                }
            }

            if (!isPanningImage && Keyboard.Modifiers == ModifierKeys.Control && ImageDisplay.IsMouseOver && !MouseControlMode && !MouseCommandMode)
            {
                this.Cursor = Cursors.Hand;  // could be better custom cursor
            }
        }

        // MouseUp: Stop Dragging
        private async void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Released)
            {
                await MiddleClickUp();
                return;
            }

            if (isPanningImage)
            {
                StopPanning();
            }

            if (isDraggingWindow)
            {
                Mouse.Capture(null);  // Release mouse capture
            }

            isDraggingWindow = false;
            isDraggingWindowFromFullscreen = false;
            await UpdateContextMenuState();  // for zoom amount, which may change when window is resized
        }

        public async Task SimulateKeyEvent(Key key, bool shift, bool control, bool alt)
        {
            bool markEndOfDuplication = control && !alt && !_duplicating && key == Key.D;

            await ProcessKeyEvent(key, shift, control, alt);

            if (markEndOfDuplication)
                _duplicating = true;
        }

        public async void External_KeyDown(object sender, KeyEventArgs e)
        {
            ModifierKeys modifiers = Keyboard.Modifiers;
            bool control = (modifiers & ModifierKeys.Control) != 0;
            bool alt = (modifiers & ModifierKeys.Alt) != 0;
            bool shift = (modifiers & ModifierKeys.Shift) != 0;

            await SimulateKeyEvent(e.Key, shift, control, alt);
        }

        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            ModifierKeys modifiers = Keyboard.Modifiers;
            bool control = (modifiers & ModifierKeys.Control) != 0;
            bool alt = (modifiers & ModifierKeys.Alt) != 0;
            bool shift = (modifiers & ModifierKeys.Shift) != 0;

            if (CommandPalette.IsVisible && CommandTextBox.IsFocused)
            {
                // Let the command palette handle this key; prevent main window hotkeys
                return;
            }

            bool markEndOfDuplication = control && !alt && !_duplicating && e.Key == Key.D;

            await ProcessKeyEvent(e.Key, shift, control, alt);

            if (markEndOfDuplication)
                _duplicating = false;

            e.Handled = true;
        }

        private bool _duplicating = false;  // TODO fix better; this is a band-aid for odd issue where hotkey D is recorded twice when duplicating specifically an open GIF.

        private async Task ProcessKeyEvent(Key key, bool shift, bool control, bool alt)
        {
            if (key == Key.F11)
            {
                await ToggleFullscreen();
                return;
            }

            if (key == Key.Escape)
            {
                if (WindowState == WindowState.Maximized)
                {
                    WindowStyle = WindowStyle.None;
                    WindowState = WindowState.Normal;
                }
                return;
            }

            if (key == Key.D)
            {
                if (control && !alt && !_duplicating)
                {
                    _duplicating = true;
                    await DuplicateWindow();
                    return;
                }
                else if (control && alt)
                {
                    DebugTextBlockBorder.Visibility = DebugTextBlockBorder.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                    return;
                }

                // do nothing for unmodified Key.D. We handle that near Key.Right later in this method.
            }

            // set window dimensions to image if possible
            if (key == Key.F)
            {
                if (control)
                {
                    ToggleFilmStrip();
                    return;
                }
                else
                {
                    autoResizingSpaceIsToggled = !autoResizingSpaceIsToggled;
                    await ResizeWindowToImage();
                    CenterWindowOnCurrentScreen();
                    return;
                }  
            }

            if (key == Key.O)
            {
                await OpenImage();
                return;
            }

            if (key == Key.Z)
            {
                if (control)
                    ZenOrUnzenAllWindows();
                else
                {
                    if (isZen)
                        RemoveZen(true);
                    else
                        Zen(isWelcome);
                }
                
                return;
            }

            if (key == Key.H)
            {
                if (control)
                    CommandPaletteRef();
                else
                    HotkeyRef();
                return;
            }

            if (key == Key.V && !(control))
            {
                MaximizeVerticalDimension();
                return;
            }

            if (key == Key.C)
            {
                if (control && alt)
                {
                    CopyCompressedImageToClipboardAsJpgFile();
                }
                else if (control)
                {
                    CopyImageToClipboard();
                }
                else
                {
                    Close();
                }

                return;
            }

            if (key == Key.P)
            {
                OpenPreferences();
                return;
            }

            if (key == Key.X)
            {
                ShowContextMenu();
                return;
            }

            if (key == Key.A && control)
            {
                About();
                return;
            }

            // Toggle Topmost (always-on-top) for this window
            if (key == Key.T)
            {
                Topmost = !Topmost;
                return;
            }

            if (key == Key.I)
            {
                ImageInfo();
                return;
            }

            if (key == Key.Q)
            {
                if (WindowState != WindowState.Maximized)
                    await ToggleCropMode();
                return;
            }

            if (key == Key.M)
            {
                if (control)
                {
                    OpenMessageHistory();
                }
                else
                {
                    MinimizeWindow();
                }

                return;
            }

            if (key == Key.R)
            {
                if (control)
                {
                    OpenRecentImagesWindow();
                }
                else
                {
                    RotateImage90Degrees();
                }

                return;
            }

            if (key == Key.B)
            {
                ResizeWindowToRemoveBestFitBars();
                return;
            }

            if (key == Key.L)
            {
                await ResizeImageToFillWindow();
                return;
            }

            if (key == Key.E)
            {
                ToggleComicMode();
                return;
            }

            if (key == Key.W)
            {
                if (!string.IsNullOrEmpty(currentlyDisplayedImagePath)){
                    if (control && alt)
                    {
                        try
                        {
                            Bitmap wallpaperBitmap = CreateImageForWallpaper();
                            WallpaperHelper.SetWallpaper(wallpaperBitmap);
                            Message("Wallpaper set from view");
                        }
                        catch
                        {
                            Message("Failed to set wallpaper from view. Make sure there are no visible margins.");
                        }
                    }
                    else if (control)
                    {
                        WallpaperHelper.SetWallpaper(currentlyDisplayedImagePath);
                        Message("Wallpaper set from image.");
                    }
                }

                return;
            }

            if (key == Key.G)
            {
                NextBackground();
                return;
            }

            if (key == Key.Space)
            {
                // bandaid fix for issue where controller gets null upon opening app directly for a GIF
                if (animationController == null && currentlyDisplayedImagePath != null)
                {
                    var animatedWebpPlugin = PluginManager.GetPluginForFiletype("webp");
                    if (animatedWebpPlugin != null && animatedWebpPlugin.SupportsFileTypes.Contains("webp") && currentlyDisplayedImagePath.ToLower().EndsWith(".webp"))
                    {
                        try
                        {
                            var ac = (ImageAnimationController?)animatedWebpPlugin.GetAnimationController(ImageDisplay);
                            animationController = ac;
                            if (ac == null)
                                throw new Exception("Got null from plugin");
                        }
                        catch (Exception ex)
                        {
                            Message("Failure retrieving animation controller: " + ex.Message);
                        }
                    }
                    else
                    {
                        animationController = ImageBehavior.GetAnimationController(ImageDisplay);
                    }
                }
                    

                // TODO changing loop preference while a gif is playing causes these controls to stop working until loading another GIF.
                if (animationController != null)
                {
                    if (control)
                    {
                        animationController.GotoFrame(0);
                    }
                    else
                    {
                        // play or pause GIF
                        if (animationController.IsComplete)
                            animationController.GotoFrame(0);
                        else if (animationController.IsPaused)
                            animationController.Play();
                        else
                            animationController.Pause();
                    }
                }

                if (VideoHost.Content is Cloudless.PluginBase.IVideoPlayer player)
                {
                    if (control && currentlyDisplayedImagePath != null)
                    {
                        //player.SetMedia(new Uri(currentlyDisplayedImagePath));
                        //await player.Play(new Uri(currentlyDisplayedImagePath));
                        player.Restart();
                    }
                    else
                    {
                        //if (player.GetDimensions() != null)  // crude check for whether media is loaded and can be played
                        //{
                            player.Pause();
                        //}
                    }
                }

                return;
            }

            if (key == Key.OemSemicolon) // && Keyboard.Modifiers == ModifierKeys.Shift)  // i.e. colon ':' but allow semicolon for convenience
            {
                if (!control && !alt)
                {
                    OpenCommandPalette();
                }
                else if (alt)  // TODO somehow, we don't get here for just ALT, even to this method apparently. Works for CTRL ALT.
                {
                    OpenPopOutCommandPaletteWindow();
                }
                else
                {
                    // Repeat previous command when CTRL is held
                    LoadCommandHistory();
                    await ExecuteCommand(CommandHistory.LastOrDefault() ?? "");
                }
            }

            // navigating in directory
            if (imageFiles != null && imageFiles.Length != 0 && !control && !alt)
            {
                // TODO for some reason, these are hit twice per key press, specifically if the image to be loaded is a WEBM. Band-aid fix is to use a flag.
                if (key == Key.Left || key == Key.A)
                {
                    await GoToPreviousImage();
                    return; 
                }
                else if (key == Key.Right || key == Key.D)
                {
                    await GoToNextImage();
                    return;
                }
            }

            if (control && !alt)
            {
                if (key == Key.OemPlus || key == Key.Add) // Zoom In
                {
                    await ZoomFromCenter(true);
                }
                else if (key == Key.OemMinus || key == Key.Subtract) // Zoom Out
                {
                    await ZoomFromCenter(false);
                }
                else if (key == Key.D0) // Reset to Best Fit
                {
                    ResetPan();
                    ResetZoom();
                }
                else if (key == Key.D9) // True Resolution (100%)
                {
                    //ResetPan();
                    ResetZoomToTrueResolution();
                }
                else if (key >= Key.D1 && key <= Key.D8) // Hotkeys for custom user commands
                {
                    switch (key)
                    {
                        case Key.D1: await RunUserCommand(0); break;
                        case Key.D2: await RunUserCommand(1); break;
                        case Key.D3: await RunUserCommand(2); break;
                        case Key.D4: await RunUserCommand(3); break;
                        case Key.D5: await RunUserCommand(4); break;
                        case Key.D6: await RunUserCommand(5); break;
                        case Key.D7: await RunUserCommand(6); break;
                        case Key.D8: await RunUserCommand(7); break;
                    }
                }
            }
            else if (!control)
            {
                if (key >= Key.D1 && key <= Key.D8) // Hotkeys for paging
                {
                    switch (key)
                    {
                        case Key.D1: SwapViewToPage(1); break;
                        case Key.D2: SwapViewToPage(2); break;
                        case Key.D3: SwapViewToPage(3); break;
                        case Key.D4: SwapViewToPage(4); break;
                        case Key.D5: SwapViewToPage(5); break;
                        case Key.D6: SwapViewToPage(6); break;
                        case Key.D7: SwapViewToPage(7); break;
                        case Key.D8: SwapViewToPage(8); break;
                    }
                }
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl)
            {
                if (!isPanningImage && ImageDisplay.IsMouseOver && !MouseControlMode && !MouseCommandMode)
                {
                    this.Cursor = Cursors.Hand;
                }
            }
        }
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                // Revert to the default cursor when Ctrl is released
                if (!isPanningImage && !MouseControlMode && !MouseCommandMode)
                {
                    this.Cursor = Cursors.Arrow;
                }
            }
        }
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }
        private async void Window_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    // Handle file drop
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files.Length > 0 && IsSupportedImageFile(files[0]))
                    {
                        await LoadImage(files[0], true);
                    }
                }
                else if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    // Handle URL drop
                    string url = (string)e.Data.GetData(DataFormats.Text);
                    if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && IsSupportedImageUri(uri))
                    {
                        DownloadAndLoadImage(uri);
                    }
                }
            }
            catch (Exception ex)
            {
                Message($"Failed to load the dragged content: {ex.Message}");
            }
        }
        private async void OnMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {
            bool comicScrollMode = Cloudless.Properties.Settings.Default.ComicModeMouseControlScroll;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || (MouseControlMode && !comicScrollMode))
            {
                if (!isExplorationMode) EnterExplorationMode();

                // Get current mouse position relative to the image
                Point cursorPosition = e.GetPosition(PrimaryWindow);

                // Zoom factor
                double zoomDelta = e.Delta > 0 ? 1.1 : 1 / 1.1;

                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                {
                    zoomDelta = e.Delta > 0 ? 1.005 : 1 / 1.005;  // finer zooming for greater precision
                }

                await Zoom(cursorPosition, zoomDelta: zoomDelta);

                e.Handled = true;
            }
            else if (MouseControlMode && comicScrollMode && isComicMode && imageTranslateTransform != null)
            {
                // In comic scroll mode, mouse wheel scrolls vertically.
                imageTranslateTransform.Y += e.Delta > 0 ? 100 : -100;

                // clamp vertically
                if (!isCropMode)
                {
                    ClampTransformToIntuitiveBounds();
                }
            }
            else
            {
                if (e.Delta > 0)
                {
                    await GoToPreviousImage();
                }
                else
                {
                    await GoToNextImage();
                }
            }
        }
        private async void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                await ToggleFullscreen();
            }
        }
        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!SkipNextContextMenu)
                ImageContextMenu.IsOpen = true;
            //else
            //    ImageContextMenu.IsOpen = false;

            //SkipNextContextMenu = false;
        }
        DispatcherTimer _middleClickDoubleClickTimer;
        private void MiddleClickDown()
        {
            _middleClickHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Settings.Default.MouseLongPressMS) };

            _middleClickHoldTimer.Tick += (s, args) =>
            {
                _middleClickHoldTimer.Stop();
                ToggleMouseCommandMode();
                //e.Handled = true;
            };
            _middleClickHoldTimer.Start();
        }
        private async Task MiddleClickUp()
        {
            bool timerStillLive = _middleClickHoldTimer?.IsEnabled ?? false;
            _middleClickHoldTimer?.Stop();

            if (_middleClickDoubleClickTimer?.IsEnabled ?? false)  // we got a second middle click within double click threshold
            {
                await DoubleMiddleClick();
            }
            else
            {
                _middleClickDoubleClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };  // 500 ms is standard in Windows
                _middleClickDoubleClickTimer.Tick += (s, args) =>
                {
                    _middleClickDoubleClickTimer?.Stop();
                };
                _middleClickDoubleClickTimer.Start();

                if (timerStillLive)
                {
                    ShortMiddleClick();
                }
            }            
        }
        private void ShortMiddleClick()
        {
            OpenQuickCommandWindow();
        }
        private async Task DoubleMiddleClick()
        {
            if (QuickCommandDisplay != null)
                CloseQuickCommandWindow();

            await ToggleCropMode();
        }
        public QuickCommandWindow? QuickCommandDisplay = null;
        private void OpenQuickCommandWindow()
        {
            if (QuickCommandDisplay != null)
                CloseQuickCommandWindow();

            var quickCommandWindow = new QuickCommandWindow(this);
            quickCommandWindow.Owner = this;
            quickCommandWindow.WindowStartupLocation = WindowStartupLocation.Manual;

            var mousePos = TargetCenterForQuickCommandDisplay;

            bool isMaximized = this.WindowState == WindowState.Maximized;
            if (isMaximized)
            {
                // TODO not quite centered due to maximized window weirdness. Can reference existing pixel hack.
                quickCommandWindow.Left = (mousePos.X) - (quickCommandWindow.Width / 2);
                quickCommandWindow.Top = (mousePos.Y) - (quickCommandWindow.Height / 2);
            }
            else
            {
                quickCommandWindow.Left = this.Left + (mousePos.X) - (quickCommandWindow.Width / 2);
                quickCommandWindow.Top = this.Top + (mousePos.Y) - (quickCommandWindow.Height / 2);
            }

                
            quickCommandWindow.Show();
            QuickCommandDisplay = quickCommandWindow;
        }
        public void CloseQuickCommandWindow()
        {
            if (QuickCommandDisplay != null)
            {
                QuickCommandDisplay.Close();
                QuickCommandDisplay = null;
            }
        }
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCLIENT = 1;
            const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTBOTTOM = 15;
            const int HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

            if (msg == WM_NCHITTEST)
            {
                // Convert mouse coordinates
                int x = lParam.ToInt32() & 0xFFFF; // LOWORD
                int y = lParam.ToInt32() >> 16;    // HIWORD
                Point mousePos = new Point(x, y);

                // Get window rectangle
                Rect windowRect = new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight);

                const int edgeThreshold = 10; // Edge detection threshold in pixels

                // Left edge
                if (mousePos.X >= windowRect.Left && mousePos.X < windowRect.Left + edgeThreshold)
                {
                    handled = true;
                    if (mousePos.Y >= windowRect.Top && mousePos.Y < windowRect.Top + edgeThreshold)
                        return (IntPtr)HTTOPLEFT;
                    if (mousePos.Y >= windowRect.Bottom - edgeThreshold && mousePos.Y <= windowRect.Bottom)
                        return (IntPtr)HTBOTTOMLEFT;
                    return (IntPtr)HTLEFT;
                }

                // Right edge
                if (mousePos.X >= windowRect.Right - edgeThreshold && mousePos.X <= windowRect.Right)
                {
                    handled = true;
                    if (mousePos.Y >= windowRect.Top && mousePos.Y < windowRect.Top + edgeThreshold)
                        return (IntPtr)HTTOPRIGHT;
                    if (mousePos.Y >= windowRect.Bottom - edgeThreshold && mousePos.Y <= windowRect.Bottom)
                        return (IntPtr)HTBOTTOMRIGHT;
                    return (IntPtr)HTRIGHT;
                }

                // Top edge
                if (mousePos.Y >= windowRect.Top && mousePos.Y < windowRect.Top + edgeThreshold)
                {
                    handled = true;
                    return (IntPtr)HTTOP;
                }

                // Bottom edge
                if (mousePos.Y >= windowRect.Bottom - edgeThreshold && mousePos.Y <= windowRect.Bottom)
                {
                    handled = true;
                    return (IntPtr)HTBOTTOM;
                }

                // Default behavior for client area
                handled = false; // Allow propagation for right-click menu
                return (IntPtr)HTCLIENT;
            }

            return IntPtr.Zero;
        }
    }
}
