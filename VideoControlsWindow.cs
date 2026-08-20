using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Cloudless.PluginBase;

namespace Cloudless
{
    public partial class VideoControlsWindow : Window
    {
        private MainWindow? _ownerWindow;
        private bool _isUserSeeking = false;
        private bool _wasPlayingBeforeSeek = false;
        private long _cachedDurationMs = -1;
        private DispatcherTimer? _positionTimer;
        private long _latestVlcPositionMs = 0;
        private long _latestVlcEventTickMs = 0;
        private double _seekDragThumbHalfWidth = 0.0;
        //private long _previousVlcPositionMs = 0;
        //private long _previousVlcEventTickMs = 0;
        private long _lastAppliedPositionMs = 0;
        private Cloudless.PluginBase.IVideoPlayer? _subscribedPlayer;
        private bool _isUserAdjustingVolume = false;
        private double _volumeDragThumbHalfWidth = 0.0;

        public VideoControlsWindow()
        {
            InitializeComponent();

            // Set initial dimensions
            Width = 800;
            // Increase height to ensure controls (button and slider thumb) are not clipped at the bottom
            Height = 64;

            // Set up event handlers
            SeekingSlider.PreviewMouseDown += SeekingSlider_PreviewMouseDown;
            SeekingSlider.PreviewMouseUp += SeekingSlider_PreviewMouseUp;
            SeekingSlider.PreviewMouseMove += SeekingSlider_PreviewMouseMove;

            InitializePositionTimer();
        }

        private void EnsureSubscribedToPlayer(IVideoPlayer player)
        {
            if (player == null) return;
            if (_subscribedPlayer == player) return;
            UnsubscribeFromPlayer();
            _subscribedPlayer = player;
            _subscribedPlayer.TimeChanged += Player_TimeChanged_Local;
        }

        private void UnsubscribeFromPlayer()
        {
            if (_subscribedPlayer != null)
            {
                _subscribedPlayer.TimeChanged -= Player_TimeChanged_Local;
                _subscribedPlayer = null;
            }
        }

        // Local handler attached directly to the IVideoPlayer.TimeChanged event.
        // Runs on the VLC thread; only stores atomic sample data and does minimal logging.
        private void Player_TimeChanged_Local(object? sender, VideoTimeChangedEventArgs e)
        {
            //try
            //{
                var player = sender as IVideoPlayer;
                if (player == null) return;
                long durationMs = (long)player.GetDuration().TotalMilliseconds;
                if (durationMs <= 0) return;
                long pos = Math.Min(e.TimeMilliseconds, durationMs);
                Interlocked.Exchange(ref _latestVlcPositionMs, pos);
                Interlocked.Exchange(ref _latestVlcEventTickMs, Environment.TickCount64);
            //}
            //catch { }
        }

        private void InitializePositionTimer()
        {
            _positionTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _positionTimer.Tick += PositionTimer_Tick;
        }

        public void StartPositionUpdates()
        {
            if (_positionTimer != null && !_positionTimer.IsEnabled)
            {
                _positionTimer.Start();
                RefreshFromVideoPlayer();
            }
        }

        public void StopPositionUpdates()
        {
            _positionTimer?.Stop();
        }

        private void PositionTimer_Tick(object? sender, EventArgs e)
        {
            if (_isUserSeeking)
                return;

            RefreshFromVideoPlayer();
        }

        private void RefreshFromVideoPlayer()
        {
            if (_isUserSeeking || _ownerWindow == null)
                return;

            var videoPlayer = _ownerWindow.VideoHost.Content as IVideoPlayer;
            if (videoPlayer == null)
                return;

            ApplyLatestPosition(videoPlayer);
            RefreshVolumeFromPlayer(videoPlayer);
            RefreshPlaybackState(videoPlayer);
        }

        private void RefreshPlaybackState(IVideoPlayer videoPlayer, bool? setTo = null)
        {
            try
            {
                bool paused = setTo != null ? setTo.Value : videoPlayer.IsPaused();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (PlayPauseButton != null)
                        {
                            // When paused, the action should be "Play"; when playing, action is "Pause"
                            PlayPauseButton.Content = paused ? "Play" : "Pause";
                        }
                    }
                    catch { }
                }));
            }
            catch { }
        }

        private void RefreshVolumeFromPlayer(IVideoPlayer videoPlayer)
        {
            try
            {
                // If user is actively dragging the volume control, do not override their input
                if (_isUserAdjustingVolume) return;

                double vol = videoPlayer.GetVolume();
                bool muted = videoPlayer.IsMuted();

                // Update UI on dispatcher (should already be on UI thread for timer but ensure safety)
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        VolumeSlider.Value = Math.Max(0, Math.Min(100, vol));
                        VolumePercentText.Text = $"{(int)Math.Round(VolumeSlider.Value)}%";
                        // Show action label: when currently muted, button should offer to "Unmute" and vice versa
                        // Update button label text to reflect the action it will perform
                        if (MuteButton != null)
                        {
                            MuteButton.Content = muted ? "Unmute" : "Mute";
                        }
                    }
                    catch { }
                }));
            }
            catch { }
        }

        private void ApplyLatestPosition(IVideoPlayer videoPlayer)
        {
            long durationMs = (long)videoPlayer.GetDuration().TotalMilliseconds;
            if (durationMs > 0)
            {
                if (_cachedDurationMs != durationMs)
                {
                    _cachedDurationMs = durationMs;
                    SeekingSlider.Maximum = durationMs;
                    DurationText.Text = FormatTime(TimeSpan.FromMilliseconds(durationMs));
                }

                // Read the latest VLC-reported position/timestamp atomically
                long latestPos = Interlocked.Read(ref _latestVlcPositionMs);
                long latestTick = Interlocked.Read(ref _latestVlcEventTickMs);

                long positionMs;
                // If player is paused, do not extrapolate — freeze at the last reported sample
                if (videoPlayer.IsPaused())
                {
                    positionMs = (latestPos > 0) ? latestPos : (long)videoPlayer.GetPosition().TotalMilliseconds;
                }
                else if (latestPos > 0 && latestTick > 0)
                {
                    // Only extrapolate from the latest sample if it's recent; otherwise rely on GetPosition()
                    long now = Environment.TickCount64;
                    long age = now - latestTick;
                    const long maxSampleAgeMs = 500; // if sample older than this, don't extrapolate
                    if (age <= maxSampleAgeMs)
                    {
                        long delta = age;
                        positionMs = (long)Math.Min(latestPos + delta, durationMs);
                    }
                    else
                    {
                        positionMs = (long)videoPlayer.GetPosition().TotalMilliseconds;
                    }
                }
                else
                {
                    positionMs = (long)videoPlayer.GetPosition().TotalMilliseconds;
                }

                if (positionMs != _lastAppliedPositionMs)
                {
                    _lastAppliedPositionMs = positionMs;
                    SeekingSlider.Value = (int)Math.Min(positionMs, durationMs);
                    CurrentTimeText.Text = FormatTime(TimeSpan.FromMilliseconds(positionMs));
                    UpdateLoopMarkers();
                }
            }
        }

        private void UpdateLoopMarkers()
        {
            if (_ownerWindow == null || MarkerCanvas == null)
                return;

            var videoPlayer = _ownerWindow.VideoHost.Content as IVideoPlayer;
            if (videoPlayer == null)
                return;

            double durationMs = Math.Max(1, videoPlayer.GetDuration().TotalMilliseconds);

            var start = _ownerWindow.VideoLoopStart;
            var end = _ownerWindow.VideoLoopEnd;

            var thumb = FindVisualChild<Thumb>(SeekingSlider);
            var track = FindVisualChild<Track>(SeekingSlider);
            double thumbWidth = thumb?.ActualWidth ?? 0.0;
            double usableWidth;
            if (track != null && track.ActualWidth > 0)
                usableWidth = Math.Max(1.0, track.ActualWidth - thumbWidth);
            else
                usableWidth = Math.Max(1.0, SeekingSlider.ActualWidth - thumbWidth);

            double thumbHalf = thumbWidth / 2.0;
            double rectHeight = StartMarker?.Height ?? 14;
            double top = Math.Max(0, (SeekingSlider.ActualHeight - rectHeight) / 2.0);

            void PositionMarker(FrameworkElement marker, TimeSpan? time)
            {
                if (marker == null) return;
                if (time.HasValue && durationMs > 0)
                {
                    double ratio = time.Value.TotalMilliseconds / durationMs;
                    ratio = Math.Max(0, Math.Min(1, ratio));
                    double effectiveX = ratio * usableWidth;
                    double left = effectiveX + thumbHalf;
                    Canvas.SetLeft(marker, left - (marker.Width / 2.0));
                    Canvas.SetTop(marker, top);
                    marker.Visibility = Visibility.Visible;
                }
                else
                {
                    marker.Visibility = Visibility.Collapsed;
                }
            }

            PositionMarker(StartMarker, start);
            PositionMarker(EndMarker, end);
        }

        public void AttachToOwner(MainWindow owner)
        {
            _ownerWindow = owner;
            this.Owner = owner;

            // Subscribe to owner window changes to reposition
            owner.LocationChanged += Owner_LocationOrSizeChanged;
            owner.SizeChanged += Owner_LocationOrSizeChanged;
            owner.StateChanged += Owner_StateChanged;

            AlignToOwner();
            // Handle keyboard input so space toggles playback when focus is within this control
            this.PreviewKeyDown += VideoControlsWindow_PreviewKeyDown;
        }

        public void DetachFromOwner()
        {
            if (_ownerWindow != null)
            {
                _ownerWindow.LocationChanged -= Owner_LocationOrSizeChanged;
                _ownerWindow.SizeChanged -= Owner_LocationOrSizeChanged;
                _ownerWindow.StateChanged -= Owner_StateChanged;
            }
            UnsubscribeFromPlayer();
            this.PreviewKeyDown -= VideoControlsWindow_PreviewKeyDown;
            _ownerWindow = null;
        }

        private void VideoControlsWindow_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
        {
            // Forward all key input to the owner window so global hotkeys work even when this control is focused.
            
            if (_ownerWindow != null)
            {
                var ps = PresentationSource.FromVisual(_ownerWindow);
                var previewArgs = new System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice, ps, Environment.TickCount, e.Key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                };
                _ownerWindow.RaiseEvent(previewArgs);

                var keyArgs = new System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice, ps, Environment.TickCount, e.Key)
                {
                    RoutedEvent = Keyboard.KeyDownEvent
                };
                _ownerWindow.RaiseEvent(keyArgs);

                // If owner handled the key, mark event handled here as well
                e.Handled = previewArgs.Handled || keyArgs.Handled;
                
            }
        }

        public void AlignToOwner()
        {
            if (_ownerWindow == null) 
                return;

            const double margin = 7;
            double targetWidth;
            double left;
            double top;

            if (_ownerWindow.WindowState == WindowState.Maximized)
            {
                IntPtr hwnd = new WindowInteropHelper(_ownerWindow).Handle;
                var mi = GetMonitorWorkArea(hwnd);
                if (mi != null)
                {
                    targetWidth = Math.Max(200, (mi.Value.Right - mi.Value.Left) - margin * 2);
                    left = mi.Value.Left + margin;
                    top = mi.Value.Bottom - this.ActualHeight - margin;
                }
                else
                {
                    var wa = SystemParameters.WorkArea;
                    targetWidth = Math.Max(200, wa.Width - margin * 2);
                    left = wa.Left + margin;
                    top = wa.Bottom - this.ActualHeight - margin;
                }
            }
            else
            {
                targetWidth = Math.Max(200, _ownerWindow.ActualWidth - margin * 2);
                left = _ownerWindow.Left + margin;
                top = _ownerWindow.Top + _ownerWindow.ActualHeight - this.ActualHeight - margin;
            }

            this.Width = targetWidth;
            this.Left = left;
            this.Top = top;
        }

        private void Owner_LocationOrSizeChanged(object? sender, EventArgs e)
        {
            AlignToOwner();
        }

        private void Owner_StateChanged(object? sender, EventArgs e)
        {
            AlignToOwner();
        }

        public void UpdateSeekingBar(IVideoPlayer videoPlayer)
        {
            if (videoPlayer == null) return;

            // Ensure we are subscribed to the player's TimeChanged events so we get raw samples
            EnsureSubscribedToPlayer(videoPlayer);

            TimeSpan duration = videoPlayer.GetDuration();
            TimeSpan position = videoPlayer.GetPosition();

            if (duration <= TimeSpan.Zero)
            {
                SeekingSlider.Maximum = 1000;
                SeekingSlider.Value = 0;
                _cachedDurationMs = -1;
                _latestVlcPositionMs = 0;
                _lastAppliedPositionMs = 0;
            }
            else
            {
                _cachedDurationMs = (long)duration.TotalMilliseconds;
                SeekingSlider.Maximum = _cachedDurationMs;
                _latestVlcPositionMs = (long)position.TotalMilliseconds;
                _lastAppliedPositionMs = -1;
                SeekingSlider.Value = (int)_latestVlcPositionMs;
            }

            CurrentTimeText.Text = FormatTime(position);
            DurationText.Text = FormatTime(duration);
            // Also refresh volume UI when we first update the seeking bar
            RefreshVolumeFromPlayer(videoPlayer);
        }

        public void OnVideoTimeChanged(VideoTimeChangedEventArgs e, IVideoPlayer? videoPlayer)
        {
            if (_isUserSeeking || videoPlayer == null)
            {
                return;
            }

            long durationMs = (long)videoPlayer.GetDuration().TotalMilliseconds;
            if (durationMs > 0)
            {
                if (_cachedDurationMs != durationMs)
                {
                    _cachedDurationMs = durationMs;
                    SeekingSlider.Maximum = durationMs;
                    DurationText.Text = FormatTime(TimeSpan.FromMilliseconds(durationMs));
                }

                Interlocked.Exchange(ref _latestVlcPositionMs, Math.Min(e.TimeMilliseconds, durationMs));
                Interlocked.Exchange(ref _latestVlcEventTickMs, Environment.TickCount64);
            }
        }

        private string FormatTime(TimeSpan timeSpan)
        {
            int totalSeconds = (int)timeSpan.TotalSeconds;
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            if (hours > 0)
                return $"{hours}:{minutes:D2}:{seconds:D2}";
            else
                return $"{minutes}:{seconds:D2}";
        }

        private void SeekingSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isUserSeeking = true;

            var videoPlayer = _ownerWindow?.VideoHost.Content as IVideoPlayer;
            if (videoPlayer != null)
            {
                // Try to infer whether the player was playing just before the seek by looking at recent time samples
                bool inferredPlaying = false;

                //long latest = Interlocked.Read(ref _latestVlcPositionMs);
                //long prev = Interlocked.Read(ref _previousVlcPositionMs);
                long latestTick = Interlocked.Read(ref _latestVlcEventTickMs);
                //long prevTick = Interlocked.Read(ref _previousVlcEventTickMs);
                long now = Environment.TickCount64;

                // Consider it playing if position advanced between recent samples and the samples are recent
                if (latestTick > 0 && now - latestTick < 1000)  // && latest > prev
                {
                    inferredPlaying = true;
                }

                // Fallback to direct check if inference failed
                if (!inferredPlaying)
                {
                    _wasPlayingBeforeSeek = !videoPlayer.IsPaused();
                }
                else
                {
                    _wasPlayingBeforeSeek = true;
                }

                if (_wasPlayingBeforeSeek)
                {
                    // Pause the video when user starts seeking
                    videoPlayer.TogglePause();
                }
            }

            // Handle click anywhere on the slider track (not just thumb drag)
            Point clickPosition = e.GetPosition(SeekingSlider);
            // Determine thumb half width and track usable width for centering
            try
            {
                var thumb = FindVisualChild<Thumb>(SeekingSlider);
                var track = FindVisualChild<Track>(SeekingSlider);
                if (thumb != null && thumb.ActualWidth > 0)
                    _seekDragThumbHalfWidth = thumb.ActualWidth / 2.0;
                else
                    _seekDragThumbHalfWidth = 0.0;

                double usableWidth;
                double posOnTrackX;
                if (track != null && track.ActualWidth > 0)
                {
                    usableWidth = Math.Max(1.0, track.ActualWidth - (thumb?.ActualWidth ?? 0.0));
                    var posOnTrack = e.GetPosition(track);
                    posOnTrackX = posOnTrack.X;
                }
                else
                {
                    // Fallback to slider dimensions
                    usableWidth = Math.Max(1.0, SeekingSlider.ActualWidth - (_seekDragThumbHalfWidth * 2.0));
                    posOnTrackX = clickPosition.X;
                }

                double effectiveX = posOnTrackX - _seekDragThumbHalfWidth;
                double ratio = effectiveX / usableWidth;
                ratio = Math.Max(0, Math.Min(1, ratio)); // Clamp to 0-1

                double newValue = ratio * (SeekingSlider.Maximum - SeekingSlider.Minimum) + SeekingSlider.Minimum;
                SeekingSlider.Value = newValue;
            }
            catch (Exception ex)
            {
                // Log and fallback to simple behavior
                System.Diagnostics.Debug.WriteLine($"SeekingSlider_PreviewMouseDown mapping failed: {ex}");
                double ratio = clickPosition.X / SeekingSlider.ActualWidth;
                ratio = Math.Max(0, Math.Min(1, ratio));
                double newValue = ratio * (SeekingSlider.Maximum - SeekingSlider.Minimum) + SeekingSlider.Minimum;
                SeekingSlider.Value = newValue;
            }

            // Capture mouse so subsequent moves while button is down will continue seeking
            SeekingSlider.CaptureMouse();
        }

        private void SeekingSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isUserSeeking = false;

            var videoPlayer = _ownerWindow?.VideoHost.Content as IVideoPlayer;
            if (videoPlayer != null)
            {
                // Convert from milliseconds back to TimeSpan
                TimeSpan targetPosition = TimeSpan.FromMilliseconds(SeekingSlider.Value);
                videoPlayer.SeekTo(targetPosition);

                // Resume playback after seeking only if it was playing before the seek began
                if (_wasPlayingBeforeSeek)
                {
                    if (videoPlayer.IsPaused())
                    {
                        videoPlayer.TogglePause();
                    }
                }
            }

            if (videoPlayer != null)
            {
                UpdateSeekingBar(videoPlayer);
            }
            SeekingSlider.ReleaseMouseCapture();
        }

        private void SeekingSlider_PreviewMouseMove(object? sender, System.Windows.Input.MouseEventArgs e)
        {
            // If user is in the middle of a seek initiated by mouse down, update the thumb position to follow the cursor
            if (!_isUserSeeking) return;

            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;

            double newValue = SeekingSlider.Value;
            try
            {
                var thumb = FindVisualChild<Thumb>(SeekingSlider);
                var track = FindVisualChild<Track>(SeekingSlider);
                double usableWidth;
                double posOnTrackX;
                if (track != null && track.ActualWidth > 0)
                {
                    usableWidth = Math.Max(1.0, track.ActualWidth - (thumb?.ActualWidth ?? 0.0));
                    var posOnTrack = e.GetPosition(track);
                    posOnTrackX = posOnTrack.X;
                }
                else
                {
                    usableWidth = Math.Max(1.0, SeekingSlider.ActualWidth - (_seekDragThumbHalfWidth * 2.0));
                    var pos = e.GetPosition(SeekingSlider);
                    posOnTrackX = pos.X;
                }

                double effectiveX = posOnTrackX - _seekDragThumbHalfWidth;
                double ratio = effectiveX / usableWidth;
                ratio = Math.Max(0, Math.Min(1, ratio));
                newValue = ratio * (SeekingSlider.Maximum - SeekingSlider.Minimum) + SeekingSlider.Minimum;
                SeekingSlider.Value = newValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SeekingSlider_PreviewMouseMove mapping failed: {ex}");
                Point pos = e.GetPosition(SeekingSlider);
                double ratio = pos.X / SeekingSlider.ActualWidth;
                ratio = Math.Max(0, Math.Min(1, ratio));
                newValue = ratio * (SeekingSlider.Maximum - SeekingSlider.Minimum) + SeekingSlider.Minimum;
                SeekingSlider.Value = newValue;
            }

            // Update current time text for immediate feedback
            var durationMs = SeekingSlider.Maximum;
            if (durationMs > 0)
            {
                var time = TimeSpan.FromMilliseconds(Math.Min(newValue, durationMs));
                CurrentTimeText.Text = FormatTime(time);
            }
        }

        // Volume control handlers
        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var videoPlayer = _ownerWindow?.VideoHost.Content as IVideoPlayer;
                if (videoPlayer == null) return;

                bool isMuted = videoPlayer.IsMuted();
                if (isMuted)
                {
                    videoPlayer.Unmute();
                }
                else
                {
                    videoPlayer.Mute();
                }

                // Immediately refresh UI
                RefreshVolumeFromPlayer(videoPlayer);
                RefreshPlaybackState(videoPlayer);
            }
            catch { }
        }

        private void VolumeSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isUserAdjustingVolume = true;

            // Compute initial thumb width for mapping
            try
            {
                var thumb = FindVisualChild<Thumb>(VolumeSlider);
                var track = FindVisualChild<Track>(VolumeSlider);
                if (thumb != null && thumb.ActualWidth > 0)
                    _volumeDragThumbHalfWidth = thumb.ActualWidth / 2.0;
                else
                    _volumeDragThumbHalfWidth = 0.0;

                double usableWidth;
                double posOnTrackX;
                if (track != null && track.ActualWidth > 0)
                {
                    usableWidth = Math.Max(1.0, track.ActualWidth - (thumb?.ActualWidth ?? 0.0));
                    var posOnTrack = e.GetPosition(track);
                    posOnTrackX = posOnTrack.X;
                }
                else
                {
                    usableWidth = Math.Max(1.0, VolumeSlider.ActualWidth - (_volumeDragThumbHalfWidth * 2.0));
                    var pos = e.GetPosition(VolumeSlider);
                    posOnTrackX = pos.X;
                }

                double effectiveX = posOnTrackX - _volumeDragThumbHalfWidth;
                double ratio = effectiveX / usableWidth;
                ratio = Math.Max(0, Math.Min(1, ratio));

                double newValue = ratio * (VolumeSlider.Maximum - VolumeSlider.Minimum) + VolumeSlider.Minimum;
                VolumeSlider.Value = newValue;
            }
            catch
            {
                Point clickPosition = e.GetPosition(VolumeSlider);
                double ratio = clickPosition.X / VolumeSlider.ActualWidth;
                ratio = Math.Max(0, Math.Min(1, ratio));
                double newValue = ratio * (VolumeSlider.Maximum - VolumeSlider.Minimum) + VolumeSlider.Minimum;
                VolumeSlider.Value = newValue;
            }

            VolumeSlider.CaptureMouse();
        }

        private void VolumeSlider_PreviewMouseMove(object? sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isUserAdjustingVolume) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;

            double newValue = VolumeSlider.Value;
            try
            {
                var thumb = FindVisualChild<Thumb>(VolumeSlider);
                var track = FindVisualChild<Track>(VolumeSlider);
                double usableWidth;
                double posOnTrackX;
                if (track != null && track.ActualWidth > 0)
                {
                    usableWidth = Math.Max(1.0, track.ActualWidth - (thumb?.ActualWidth ?? 0.0));
                    var posOnTrack = e.GetPosition(track);
                    posOnTrackX = posOnTrack.X;
                }
                else
                {
                    usableWidth = Math.Max(1.0, VolumeSlider.ActualWidth - (_volumeDragThumbHalfWidth * 2.0));
                    var pos = e.GetPosition(VolumeSlider);
                    posOnTrackX = pos.X;
                }

                double effectiveX = posOnTrackX - _volumeDragThumbHalfWidth;
                double ratio = effectiveX / usableWidth;
                ratio = Math.Max(0, Math.Min(1, ratio));
                newValue = ratio * (VolumeSlider.Maximum - VolumeSlider.Minimum) + VolumeSlider.Minimum;
                VolumeSlider.Value = newValue;
            }
            catch
            {
                Point pos = e.GetPosition(VolumeSlider);
                double ratio = pos.X / VolumeSlider.ActualWidth;
                ratio = Math.Max(0, Math.Min(1, ratio));
                newValue = ratio * (VolumeSlider.Maximum - VolumeSlider.Minimum) + VolumeSlider.Minimum;
                VolumeSlider.Value = newValue;
            }

            // Update percent label live
            VolumePercentText.Text = $"{(int)Math.Round(VolumeSlider.Value)}%";
        }

        private void VolumeSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isUserAdjustingVolume) return;
            _isUserAdjustingVolume = false;

            var videoPlayer = _ownerWindow?.VideoHost.Content as IVideoPlayer;
            if (videoPlayer != null)
            {
                int vol = (int)Math.Round(VolumeSlider.Value);
                videoPlayer.SetVolume(vol);
                VolumePercentText.Text = $"{vol}%";
            }

            VolumeSlider.ReleaseMouseCapture();
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var videoPlayer = _ownerWindow?.VideoHost.Content as IVideoPlayer;
                if (videoPlayer == null) return;

                // Toggle pause/play
                videoPlayer.TogglePause();

                // Update UI immediately
                RefreshPlaybackState(videoPlayer);  // TODO use setTo within, for snappier UX perhaps
            }
            catch { }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private RECT? GetMonitorWorkArea(IntPtr hwnd)
        {
            //try
            //{
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return null;

            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(mi);
            if (GetMonitorInfo(monitor, ref mi))
            {
                return mi.rcWork;
            }
            //}
            //catch (Exception ex)
            //{
            //    Debug.WriteLine($"GetMonitorWorkArea failed: {ex}");
            //}
            return null;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
