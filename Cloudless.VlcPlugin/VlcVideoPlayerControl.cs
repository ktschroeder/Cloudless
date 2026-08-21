using Cloudless.PluginBase;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace Cloudless.VlcPlugin
{
    public class VlcVideoPlayerControl : UserControl, IDisposable, IVideoPlayer
    {
        // Implement the TimeChanged event from IVideoPlayer
        public event EventHandler<Cloudless.PluginBase.VideoTimeChangedEventArgs>? TimeChanged;

        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private VideoView _videoView;
        private Grid _videoHostContainer;
        private IntPtr _videoChildHwnd = IntPtr.Zero;
        private IntPtr? _preloadedLibVlcHandle = null;
        private IntPtr? _preloadedLibVlcCoreHandle = null;

        private Uri _currentUri;
        private Media _currentMedia = null;
        private TimeSpan? _loopStart = null;
        private TimeSpan? _loopEnd = null;
        private DateTime _lastLoopSeek = DateTime.MinValue;

        TaskCompletionSource<bool> _loadSignal;

        // Video pan/zoom state maintained locally. We'll attempt to apply these to LibVLC if supported; otherwise apply to the VideoView transform as a fallback.
        private double _videoScale = 1.0;
        private double _videoPanX = 0.0;
        private double _videoPanY = 0.0;
        // Whether to attempt calling into native LibVLC scale APIs. Disabled by default because some
        // native calls can reposition/center video unexpectedly. Enable only after confirming behavior.
        private bool _preferNativeScale = false;
       //private bool _preferNativeScale = true; // Temporarily enable native scaling flag by default to test native behavior.

        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr AddDllDirectory(string lpPathName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(uint DirectoryFlags);

        public VlcVideoPlayerControl()
        {
        }

        // Native interop helpers to find and move the native video child window
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;

        //private void TryApplyNativeWindowTransform()
        //{
        //        if (_videoHostContainer == null) return;

        //        // Ensure we have parent HWND
        //        var ps = System.Windows.PresentationSource.FromVisual(_videoHostContainer) as System.Windows.Interop.HwndSource;
        //        if (ps == null) return;
        //        IntPtr parentHwnd = ps.Handle;
        //        if (parentHwnd == IntPtr.Zero) return;

        //        // Find child hwnd if not cached
        //        if (_videoChildHwnd == IntPtr.Zero || !IsWindow(_videoChildHwnd))
        //        {
        //            IntPtr found = IntPtr.Zero;
        //            EnumChildWindows(parentHwnd, (h, l) =>
        //            {
        //                // pick first visible child
        //                found = h;
        //                return false; // stop enumeration
        //            }, IntPtr.Zero);

        //            if (found != IntPtr.Zero)
        //                _videoChildHwnd = found;
        //        }

        //        if (_videoChildHwnd == IntPtr.Zero || !IsWindow(_videoChildHwnd)) return;

        //        // Compute desired child rectangle in screen coords based on host container
        //        if (!GetWindowRect(parentHwnd, out RECT parentRect)) return;

        //        double hostW = _videoHostContainer.ActualWidth;
        //        double hostH = _videoHostContainer.ActualHeight;
        //        if (hostW <= 0 || hostH <= 0) return;

        //        int width = (int)Math.Round(hostW * _videoScale);
        //        int height = (int)Math.Round(hostH * _videoScale);

        //        int left = parentRect.Left + (int)Math.Round((hostW - width) / 2.0 + _videoPanX);
        //        int top = parentRect.Top + (int)Math.Round((hostH - height) / 2.0 + _videoPanY);

        //        // Apply position/size
        //        SetWindowPos(_videoChildHwnd, HWND_TOP, left, top, Math.Max(1, width), Math.Max(1, height), SWP_NOZORDER | SWP_SHOWWINDOW);
        //}

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        //private const uint RDW_INVALIDATE = 0x0001;
        //private const uint RDW_UPDATENOW = 0x0100;
        //private const uint RDW_ERASE = 0x0004;

        public void SetLoopRange(TimeSpan? start, TimeSpan? end)
        {
            _loopStart = start;
            _loopEnd = end;

            // If a loop start is provided, try to seek to it now so playback begins at the loop boundary.
            if (start.HasValue)
            {
                // If media view has not finished loading yet, wait for load signal then seek.
                try
                {
                    Action seekAction = () =>
                    {
                        try
                        {
                            if (_mediaPlayer != null && _mediaPlayer.IsSeekable)
                            {
                                _mediaPlayer.SeekTo(start.Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error seeking to loop start: {ex.Message}");
                        }
                    };

                    if (_loadSignal != null && !_loadSignal.Task.IsCompleted)
                    {
                        _ = _loadSignal.Task.ContinueWith(t =>
                        {
                            Application.Current.Dispatcher.BeginInvoke(seekAction);
                        });
                    }
                    else
                    {
                        Application.Current.Dispatcher.BeginInvoke(seekAction);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SetLoopRange error: {ex.Message}");
                }
            }

            // Also attempt to position/resize the native child HWND so the native video surface updates immediately.
            //TryApplyNativeWindowTransform();
        }



        public async Task Initialize()
        {
            Cloudless.Diagnostics.LeakTracker.Register(this, "VlcVideoPlayerControl");

            _loadSignal = new TaskCompletionSource<bool>();

            _libVLC = await LibVlcProvider.GetInstance();

            _videoView = new VideoView
            {
                //MediaPlayer = _mediaPlayer,
                //HorizontalAlignment = HorizontalAlignment.Stretch,
                //VerticalAlignment = VerticalAlignment.Stretch
            };
            // Use MediaPlayer.TimeChanged event for precise loop detection instead of polling timer.
            // TimeChanged will be subscribed when media player is created.
            // we need the VideoView to be fully loaded before setting a MediaPlayer on it.
            _videoView.Loaded += VideoView_Loaded;
            this.Unloaded += VlcVideoPlayerControl_Unloaded;

            // Wrap the VideoView in a container so we can apply transforms to the container
            _videoHostContainer = new Grid();
            _videoHostContainer.Children.Add(_videoView);
            // Monitor layout/size/transform changes to help diagnose unexpected recentering behavior
            _videoHostContainer.LayoutUpdated += (s, e) =>
            {
                try
                {
                    var rt = _videoHostContainer.RenderTransform;
                    if (rt is TransformGroup tg)
                    {
                        var st = tg.Children.OfType<ScaleTransform>().FirstOrDefault();
                        var tt = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                        Console.WriteLine($"[VLC] LayoutUpdated: scale={st?.ScaleX:F3}/{st?.ScaleY:F3} pan={tt?.X:F1},{tt?.Y:F1} hostSize={_videoHostContainer.ActualWidth:F0}x{_videoHostContainer.ActualHeight:F0}");
                    }
                    else if (rt is ScaleTransform srt)
                    {
                        Console.WriteLine($"[VLC] LayoutUpdated: scale={srt.ScaleX:F3}/{srt.ScaleY:F3} hostSize={_videoHostContainer.ActualWidth:F0}x{_videoHostContainer.ActualHeight:F0}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VLC] LayoutUpdated error: {ex.Message}");
                }
            };
            Content = _videoHostContainer;
        }

        private void VlcVideoPlayerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Defensive: ensure we try to stop playback if the control is unloaded from visual tree.
            Stop();
        }

        private async void VideoView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _libVLC = await LibVlcProvider.GetInstance();
                _mediaPlayer = new MediaPlayer(_libVLC);

                Cloudless.Diagnostics.LeakTracker.Register(_mediaPlayer, "LibVLC.MediaPlayer");

                // subscribe to time changed for precise loop handling
                _mediaPlayer.TimeChanged += MediaPlayer_TimeChanged;

                _mediaPlayer.EndReached += (sender, args) =>
                {
                    try
                    {
                        // Note: App seems to crash here sometimes when this event is triggered but the window has been closed. I think in the QueueUserWorkItem method.

                        // IMPORTANT: Restart playback on a different thread to avoid deadlocks
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            //_mediaPlayer.Stop(); // Recommended to stop before re-playing
                            _mediaPlayer.Play(new Media(_libVLC, _currentUri));  // TODO explore hacks for smoth looping... https://stackoverflow.com/questions/56487740/how-to-achieve-looping-playback-with-libvlcsharp  // media.add_option(":input-repeat=65535")
                                                                                 //_videoView.MediaPlayer = _mediaPlayer2;
                                                                                 //_mediaPlayer2.Play();
                        });

                        //Restart();
                    }
                    catch (Exception ex)
                    {
                        // TODO probably pass in messenger to plugins to be used like here
                        Console.WriteLine($"Error in EndReached handler: {ex.Message}");
                    }
                };

                _mediaPlayer.EnableMouseInput = false;
                _mediaPlayer.EnableKeyInput = false;

                // No EndReached handler here to avoid captured closures keeping media player alive.
                _videoView.MediaPlayer = _mediaPlayer;

                _loadSignal.SetResult(true);
            }
            catch (Exception ex)
            {
                // signal load to avoid deadlocks
                _loadSignal.TrySetResult(true);
                Console.WriteLine($"VlcVideoPlayerControl.VideoView_Loaded failed: {ex.Message}");
            }
        }

        private void MediaPlayer_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            try
            {
                // Marshal event invocation to UI thread so subscribers may access WPF safely.
                var timeMs = e.Time;
                try
                {
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            TimeChanged?.Invoke(this, new Cloudless.PluginBase.VideoTimeChangedEventArgs { TimeMilliseconds = timeMs });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"TimeChanged handler threw: {ex.Message}");
                        }
                    }));
                }
                catch
                {
                    // If Application or Dispatcher not available, fallback to direct invoke (best-effort)
                    try { TimeChanged?.Invoke(this, new Cloudless.PluginBase.VideoTimeChangedEventArgs { TimeMilliseconds = timeMs }); } catch { }
                }

                if (_mediaPlayer == null) return;
                if (!_loopEnd.HasValue) return;

                long currentMs = timeMs; // milliseconds
                if (currentMs < 0) return;

                long endMs = (long)_loopEnd.Value.TotalMilliseconds;

                if (currentMs >= endMs)
                {
                    var start = _loopStart ?? TimeSpan.Zero;
                    // Seek on UI thread to avoid threading issues
                    try
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (_mediaPlayer != null && _mediaPlayer.IsSeekable)
                                {
                                    _mediaPlayer.SeekTo(start);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error seeking to loop start: {ex.Message}");
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"MediaPlayer_TimeChanged dispatch error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MediaPlayer_TimeChanged error: {ex.Message}");
            }
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                try
                {
                    File.Copy(file, destFile, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to copy '{file}' to '{destFile}': {ex.Message}");
                }
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSub = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectoryRecursive(dir, destSub);
            }
        }

        public async Task Play(Uri uri, Task? postPlayTask = null)
        {
            await _loadSignal.Task;  // ensure video view is loaded, or else VLC will open the media in an external player
            // Additionally ensure the VideoView has been attached to a PresentationSource (HWND) so LibVLC
            // will use the WPF host instead of creating an external native window. In some timing scenarios
            // Loaded can fire before the native handle is ready, especially when creating many windows quickly.
            await EnsureVideoViewReadyAsync();

            try
            {
                _mediaPlayer?.Stop();
                _currentMedia?.Dispose();
                _currentMedia = null;

                // Create and keep the media for the lifetime of playback so we can reliably dispose it later.
                var media = new Media(_libVLC, uri);
                _currentMedia = media;

                Cloudless.Diagnostics.LeakTracker.Register(media, "LibVLC.Media");

                // If a different URI is being played, reset any loop ranges
                if (_currentUri == null || !_currentUri.Equals(uri))
                {
                    _loopStart = null;
                    _loopEnd = null;
                }

                _currentUri = uri;
                _mediaPlayer?.Play(media);

                if (postPlayTask != null)
                {
                    await postPlayTask;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VlcVideoPlayerControl.Play failed: {ex.Message}");
                throw;
            }
        }

        private async Task EnsureVideoViewReadyAsync(int timeoutMs = 500)
        {
            if (_videoView == null) return;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                bool ready = false;
                try
                {
                    // Query visual state on the UI thread to get reliable results
                    await _videoView.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            // Prefer HwndSource which exposes the native handle; PresentationSource may be non-null earlier
                            var ps2 = System.Windows.PresentationSource.FromVisual(_videoView);
                            if (ps2 is System.Windows.Interop.HwndSource hwndSrc && hwndSrc.Handle != IntPtr.Zero)
                            {
                                ready = true;
                                return;
                            }

                            // Also ensure the control and its containing window are visible
                            if (_videoView.IsLoaded && _videoView.IsVisible)
                            {
                                var win = Window.GetWindow(_videoView);
                                if (win != null && win.IsVisible)
                                {
                                    // If PresentationSource exists we consider it ready
                                    var ps = System.Windows.PresentationSource.FromVisual(_videoView);
                                    if (ps != null)
                                    {
                                        ready = true;
                                        return;
                                    }
                                }
                            }
                        }
                        catch { }
                    }, System.Windows.Threading.DispatcherPriority.Render).Task.ConfigureAwait(false);
                }
                catch { }

                if (ready) return;

                if (sw.ElapsedMilliseconds > timeoutMs)
                    return; // give up after timeout; we'll try to play anyway

                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        public async Task<(int, int)?> GetDimensions()
        {
            await _loadSignal.Task;  // ensure video view is loaded, or else _mediaPlayer is probably null

            if (_mediaPlayer?.Media == null)
                return null;

            var tracks = _mediaPlayer.Media.Tracks;
            foreach (var track in tracks)
            {
                if (track.TrackType == TrackType.Video)
                {
                    var videoTrack = track.Data;
                    var width = videoTrack.Video.Width;
                    var height = videoTrack.Video.Height;
                    if (width > 0 && height > 0)
                        return ((int)width, (int)height);
                }
            }
            return null;
        }

        public void TogglePause()
        {
            _mediaPlayer?.SetPause(_mediaPlayer.IsPlaying);
        }

        public void SetVideoZoom(double scale, double centerX, double centerY)
        {
            // compute new scale but preserve old scale value for delta computations
            double oldScale = _videoScale;
            double newScale = Math.Max(0.01, scale);
            Console.WriteLine($"[VLC] SetVideoZoom called: scaleRequested={scale:F3} newScale={newScale:F3} oldScale={oldScale:F3} center=({centerX:F1},{centerY:F1}) preferNative={_preferNativeScale}");
            // assign new scale
            _videoScale = newScale;

            // As a fallback, try applying a WPF transform to the VideoView (may not affect native surface due to airspace).
            if (_videoHostContainer == null)
                return;

            Console.WriteLine($"[VLC] Applying transform: newScale={newScale:F3} _videoPan=({_videoPanX:F1},{_videoPanY:F1}) hostSize={_videoHostContainer.ActualWidth:F0}x{_videoHostContainer.ActualHeight:F0}");
            // Ensure transforms exist on the host container and use center origin so scaling behaves like images
            var tg = _videoHostContainer.RenderTransform as TransformGroup;
            if (tg == null)
            {
                tg = new TransformGroup();
                tg.Children.Add(new ScaleTransform(1, 1));
                tg.Children.Add(new TranslateTransform(0, 0));
                _videoHostContainer.RenderTransform = tg;
                _videoHostContainer.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            var st = tg.Children.OfType<ScaleTransform>().FirstOrDefault();
            var tt = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
            if (st == null)
            {
                st = new ScaleTransform(1, 1);
                tg.Children.Insert(0, st);
            }
            if (tt == null)
            {
                tt = new TranslateTransform(0, 0);
                tg.Children.Add(tt);
            }

            // Compute derivedDelta (newScale / oldScale) using captured variables
            double derivedDelta = (oldScale > 0) ? (newScale / oldScale) : 1.0;

            // Determine zoom origin in local (pre-transform) coordinates relative to the host container
            Point mouseLocal = System.Windows.Input.Mouse.GetPosition(_videoHostContainer);
            double localX = mouseLocal.X;
            double localY = mouseLocal.Y;

            localX = centerX;  // TODO remove above if not using.
            localY = centerY;

            var rt2 = _videoHostContainer.RenderTransform;
            if (rt2 != null && !rt2.Value.IsIdentity)
            {
                var m2 = rt2.Value;
                if (m2.HasInverse)
                {
                    m2.Invert();
                    var pre = m2.Transform(mouseLocal);
                    localX = pre.X;
                    localY = pre.Y;
                }
            }

            // Use host container actual size as the container for pan math
            double containerWidth = _videoHostContainer.ActualWidth;
            double containerHeight = _videoHostContainer.ActualHeight;

            // Calculate offset relative to the center of the host container
            double offsetX = localX - _videoPanX - (containerWidth / 2.0);
            double offsetY = localY - _videoPanY - (containerHeight / 2.0);

            // Adjust pan so zoom is centered on the provided point
            _videoPanX -= offsetX * (derivedDelta - 1.0);
            _videoPanY -= offsetY * (derivedDelta - 1.0);

            // Apply scale and pan values using the same coordinate system as image zooming
            st.ScaleX = newScale;
            st.ScaleY = newScale;

            // apply pan adjustments
            tt.X = _videoPanX;
            tt.Y = _videoPanY;

            // persist pan
            _videoPanX = tt.X;
            _videoPanY = tt.Y;
            Console.WriteLine($"[VLC] Applied transform: scale={st.ScaleX:F3} pan=({tt.X:F1},{tt.Y:F1})");

            // Force render pass
            //_videoHostContainer.Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.Render);
        }

        public void PanVideoBy(double deltaX, double deltaY)
        {
            _videoPanX += deltaX;
            _videoPanY += deltaY;

            Console.WriteLine($"[VLC] PanVideoBy called: delta=({deltaX:F1},{deltaY:F1}) -> pan=({_videoPanX:F1},{_videoPanY:F1})");

            if (_videoHostContainer == null)
                return;

            // Try to apply as a RenderTransform
            _videoHostContainer.Dispatcher.BeginInvoke(new Action(() =>
            {
                var tg = _videoHostContainer.RenderTransform as TransformGroup;
                if (tg == null)
                {
                    tg = new TransformGroup();
                    tg.Children.Add(new ScaleTransform(_videoScale, _videoScale));
                    tg.Children.Add(new TranslateTransform(_videoPanX, _videoPanY));
                    _videoHostContainer.RenderTransform = tg;
                }
                else
                {
                    var tt = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                    if (tt == null)
                    {
                        tt = new TranslateTransform(_videoPanX, _videoPanY);
                        tg.Children.Add(tt);
                    }
                    else
                    {
                        tt.X = _videoPanX;
                        tt.Y = _videoPanY;
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        public double GetVideoZoom()
        {
            return _videoScale;
        }

        public (double, double) GetVideoPan()
        {
            return (_videoPanX, _videoPanY);
        }

        public void ResetVideoPanZoom()
        {
            _videoScale = 1.0;
            _videoPanX = 0.0;
            _videoPanY = 0.0;

            if (_mediaPlayer != null)
            {
                var mi = _mediaPlayer.GetType().GetMethod("SetScale", new Type[] { typeof(float) });
                if (mi != null)
                {
                    mi.Invoke(_mediaPlayer, new object[] { (float)_videoScale });
                }
            }

            if (_videoHostContainer != null)
            {
                _videoHostContainer.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _videoHostContainer.RenderTransform = Transform.Identity;
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        public void Stop()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Stop();
                _currentMedia?.Dispose();
                _currentMedia = null;
            }
        }

        public void SetMedia(Uri uri)
        {
            // Replace the current media without starting playback.
            //try
            //{
            //    var media = new Media(_libVLC, uri);
            //    try
            //    {
            //        _currentMedia?.Dispose();
            //    }
            //    catch { }
            //    _currentMedia = media;
            //    try { LeakTracker.Register(media, "LibVLC.Media"); } catch { }
            //    _mediaPlayer?.SetMedia(media);
            //}
            //catch { }
        }

        public void Restart()
        {
            if (_mediaPlayer == null || _mediaPlayer.Media == null)
                return;

            if (_mediaPlayer.IsSeekable)
            {
                TimeSpan start = TimeSpan.Zero;
                _mediaPlayer?.SeekTo(start);
            }
            else
            {
                _mediaPlayer?.Stop();
                _mediaPlayer?.Play();
            }
        }

        public TimeSpan GetPosition()
        {
            // return current playback time in TimeSpan. If not available, return TimeSpan.Zero
            if (_mediaPlayer == null)
                return TimeSpan.Zero;

            // LibVLC MediaPlayer.Time is long milliseconds
            try
            {
                long ms = _mediaPlayer.Time;
                if (ms < 0) return TimeSpan.Zero;
                return TimeSpan.FromMilliseconds(ms);
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        public void SeekTo(TimeSpan position)
        {
            if (_mediaPlayer == null)
                return;

            // Clamp to valid range if possible
            try
            {
                if (_mediaPlayer.IsSeekable)
                {
                    // Do not allow seeking before the configured loop start
                    if (_loopStart.HasValue && position < _loopStart.Value)
                        position = _loopStart.Value;

                    if (position < TimeSpan.Zero)
                        position = TimeSpan.Zero;

                    // If length available, clamp to it
                    long lengthMs = _mediaPlayer.Length;
                    if (lengthMs > 0 && position.TotalMilliseconds > lengthMs)
                        position = TimeSpan.FromMilliseconds(lengthMs);

                    _mediaPlayer?.SeekTo(position);
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (_videoView != null)
            {
                _videoView.Loaded -= VideoView_Loaded;
                this.Unloaded -= VlcVideoPlayerControl_Unloaded;

                if (_mediaPlayer != null)
                {
                    _mediaPlayer.TimeChanged -= MediaPlayer_TimeChanged;
                }

                // Detach MediaPlayer from VideoView (MediaPlayer may be same as _mediaPlayer)
                if (_videoView.MediaPlayer != null)
                {
                    _videoView.MediaPlayer.Stop();
                    _videoView.MediaPlayer.Dispose();
                    _videoView.MediaPlayer = null;
                }

                _videoView.Dispose();
            }

            // Stop and dispose managed media objects
            if (_currentMedia != null)
            {
                _currentMedia.Dispose();
                _currentMedia = null;
            }

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }

            // Do NOT dispose the shared LibVLC instance provided by LibVlcProvider; it is shared across players.
            _libVLC = null;

            if (_preloadedLibVlcHandle.HasValue)
                NativeLibrary.Free(_preloadedLibVlcHandle.Value);
            if (_preloadedLibVlcCoreHandle.HasValue)
                NativeLibrary.Free(_preloadedLibVlcCoreHandle.Value);

            Cloudless.Diagnostics.LeakTracker.MarkClosed(this);
        }

        public TimeSpan GetDuration()
        {
            if (_mediaPlayer == null)
                return TimeSpan.Zero;

            try
            {
                long lengthMs = _mediaPlayer.Length;
                if (lengthMs <= 0)
                    return TimeSpan.Zero;
                return TimeSpan.FromMilliseconds(lengthMs);
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        public void SeekFineForward()
        {
            if (_mediaPlayer == null)
                return;
            try
            {
                // VLC supports frame stepping forward
                _mediaPlayer.NextFrame();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SeekFineForward error: {ex.Message}");
            }
        }

        public void SeekFineBackward()
        {
            if (_mediaPlayer == null)
                return;
            try
            {
                // VLC does not support frame stepping backward, so seek back a small amount
                var current = GetPosition();
                var target = current - TimeSpan.FromMilliseconds(2);  // 1 has no effect. 2 is best available, apparently. It's fine.
                if (target < TimeSpan.Zero)
                    target = TimeSpan.Zero;
                SeekTo(target);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SeekFineBackward error: {ex.Message}");
            }
        }

        public void Mute()
        {
            if (_mediaPlayer == null)
                return;
            try
            {
                _mediaPlayer.Mute = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mute error: {ex.Message}");
            }
        }

        public void Unmute ()
        {
            if (_mediaPlayer == null)
                return;
            try
            {
                _mediaPlayer.Mute = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unmute error: {ex.Message}");
            }
        }

        public bool IsMuted()
        {
            if (_mediaPlayer == null)
                return false;
            try
            {
                return _mediaPlayer.Mute;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IsMuted error: {ex.Message}");
                return false;
            }
        }

        public bool IsPaused()
        {
            if (_mediaPlayer == null)
                return false;
            try
            {
                return !_mediaPlayer.IsPlaying;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IsPaused error: {ex.Message}");
                return false;
            }
        }

        public void SetVolume(double volume)
        {
            if (_mediaPlayer == null)
                return;
            try
            {
                // VLC volume is 0-100, so clamp and convert
                int vol = (int)Math.Round(Math.Max(0, Math.Min(100, volume)));
                _mediaPlayer.Volume = vol;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetVolume error: {ex.Message}");
            }
        }

        public double GetVolume()
        {
            if (_mediaPlayer == null)
                return 0.0;
            try
            {
                return _mediaPlayer.Volume;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetVolume error: {ex.Message}");
                return 0.0;
            }
        }

    }
}