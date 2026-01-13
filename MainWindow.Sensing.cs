using System.Windows;
using System.Windows.Input;
using WpfAnimatedGif;

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
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (!isExplorationMode) EnterExplorationMode();

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
        private void Window_MouseMove(object sender, MouseEventArgs e)
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
                ToggleFullscreen();
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

            if (!isPanningImage && Keyboard.Modifiers == ModifierKeys.Control && ImageDisplay.IsMouseOver)
            {
                this.Cursor = Cursors.Hand;  // could be better custom cursor
            }
        }

        // MouseUp: Stop Dragging
        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
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
            UpdateContextMenuState();  // for zoom amount, which may change when window is resized
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (CommandPalette.IsVisible && CommandTextBox.IsFocused)
            {
                // Let the command palette handle this key; prevent main window hotkeys
                return;
            }

            if (e.Key == Key.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (WindowState == WindowState.Maximized)
                {
                    WindowStyle = WindowStyle.None;
                    WindowState = WindowState.Normal;
                }
                e.Handled = true;
                return;
            }


            if (e.Key == Key.D)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    DebugTextBlockBorder.Visibility = DebugTextBlockBorder.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                }
                else
                {
                    DuplicateWindow();
                }
                e.Handled = true;
                return;
            }


            // set window dimensions to image if possible
            if (e.Key == Key.F)
            {
                autoResizingSpaceIsToggled = !autoResizingSpaceIsToggled;
                ResizeWindowToImage();
                CenterWindow();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.O)
            {
                OpenImage();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Z)
            {
                if (isZen)
                    RemoveZen(true);
                else
                    Zen(isWelcome);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.H)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    CommandPaletteRef();
                else
                    HotkeyRef();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.V && !(Keyboard.Modifiers == ModifierKeys.Control))
            {
                MaximizeVerticalDimension();
                e.Handled = true;
                return;
            }


            if (e.Key == Key.C)
            {
                ModifierKeys modifiers = Keyboard.Modifiers;

                if ((modifiers & ModifierKeys.Control) != 0 && (modifiers & ModifierKeys.Alt) != 0)
                {
                    CopyCompressedImageToClipboardAsJpgFile();
                }
                else if ((modifiers & ModifierKeys.Control) != 0)
                {
                    CopyImageToClipboard();
                }
                else
                {
                    Close();
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.P)
            {
                OpenPreferences();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.X)
            {
                ShowContextMenu();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.A)
            {
                About();
                e.Handled = true;
                return;
            }

            // Toggle Topmost (always-on-top) for this window
            if (e.Key == Key.T)
            {
                Topmost = !Topmost;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.I)
            {
                ImageInfo();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Q)
            {
                if (WindowState != WindowState.Maximized)
                    ToggleCropMode();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.M)
            {
                MinimizeWindow();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.R)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    OpenRecentImagesWindow();
                }
                else
                {
                    RotateImage90Degrees();
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.B)
            {
                ResizeWindowToRemoveBestFitBars();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.G)
            {
                NextBackground();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Space)
            {
                // bandaid fix for issue where controller gets null upon opening app directly for a GIF
                if (gifController == null && currentlyDisplayedImagePath != null && currentlyDisplayedImagePath.ToLower().EndsWith(".gif"))
                    gifController = ImageBehavior.GetAnimationController(ImageDisplay);

                // TODO changing loop preference while a gif is playing causes these controls to stop working until loading another GIF.
                if (gifController != null)
                {
                    ModifierKeys modifiers = Keyboard.Modifiers;
                    if ((modifiers & ModifierKeys.Control) != 0)
                    {
                        gifController.GotoFrame(0);
                    }
                    else
                    {
                        // play or pause GIF
                        if (gifController.IsComplete)
                            gifController.GotoFrame(0);
                        else if (gifController.IsPaused)
                            gifController.Play();
                        else
                            gifController.Pause();
                    }
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.OemSemicolon) // && Keyboard.Modifiers == ModifierKeys.Shift)  // i.e. colon ':' but allow semicolon for convenience
            {
                if (Keyboard.Modifiers != ModifierKeys.Control)
                {
                    OpenCommandPalette();
                }
                else
                {
                    // Repeat previous command when CTRL is held
                    ExecuteCommand(_commandHistory.LastOrDefault() ?? "");
                }

                e.Handled = true;
            }

            // navigating in directory
            if (imageFiles != null && imageFiles.Length != 0)
            {
                if (e.Key == Key.Left)
                {
                    // Go to the previous image
                    currentImageIndex = (currentImageIndex == 0) ? imageFiles.Length - 1 : currentImageIndex - 1;
                    DisplayImage(currentImageIndex, true);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Right)
                {
                    // Go to the next image
                    currentImageIndex = (currentImageIndex == imageFiles.Length - 1) ? 0 : currentImageIndex + 1;
                    DisplayImage(currentImageIndex, true);
                    e.Handled = true;
                    return;
                }
            }

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.OemPlus || e.Key == Key.Add) // Zoom In
                {
                    ZoomFromCenter(true);
                    e.Handled = true;
                }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) // Zoom Out
                {
                    ZoomFromCenter(false);
                    e.Handled = true;
                }
                else if (e.Key == Key.D0) // Reset to Best Fit
                {
                    ResetPan();
                    ResetZoom();
                    e.Handled = true;
                }
                else if (e.Key == Key.D9) // True Resolution (100%)
                {
                    //ResetPan();
                    ResetZoomToTrueResolution();
                    e.Handled = true;
                }
                else if (e.Key >= Key.D1 && e.Key <= Key.D8) // Hotkeys for custom user commands
                {
                    switch (e.Key)
                    {
                        case Key.D1: RunUserCommand(0); break;
                        case Key.D2: RunUserCommand(1); break;
                        case Key.D3: RunUserCommand(2); break;
                        case Key.D4: RunUserCommand(3); break;
                        case Key.D5: RunUserCommand(4); break;
                        case Key.D6: RunUserCommand(5); break;
                        case Key.D7: RunUserCommand(6); break;
                        case Key.D8: RunUserCommand(7); break;
                    }
                    e.Handled = true;
                }
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                if (!isPanningImage && ImageDisplay.IsMouseOver)
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
                if (!isPanningImage)
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
        private void Window_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    // Handle file drop
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files.Length > 0 && IsSupportedImageFile(files[0]))
                    {
                        LoadImage(files[0], true);
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
                MessageBox.Show($"Failed to load the dragged content: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void OnMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
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

                Zoom(cursorPosition, zoomDelta: zoomDelta);

                e.Handled = true;
            }
        }
        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                ToggleFullscreen();
            }
        }
        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ImageContextMenu.IsOpen = true;
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
