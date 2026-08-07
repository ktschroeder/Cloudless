using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Threading;
using Cloudless.PluginBase;

namespace Cloudless
{
    public partial class VideoControlsWindow : Window
    {
        private MainWindow? _ownerWindow;
        private bool _isUserSeeking = false;
        private long _cachedDurationMs = -1;
        private DispatcherTimer? _positionTimer;
        private long _latestVlcPositionMs = 0;
        private long _latestVlcEventTickMs = 0;
        private long _lastAppliedPositionMs = 0;
        private Cloudless.PluginBase.IVideoPlayer? _subscribedPlayer;

        public VideoControlsWindow()
        {
            InitializeComponent();

            // Set initial dimensions
            Width = 800;
            Height = 55;

            // Set up event handlers
            SeekingSlider.PreviewMouseDown += SeekingSlider_PreviewMouseDown;
            SeekingSlider.PreviewMouseUp += SeekingSlider_PreviewMouseUp;

            InitializePositionTimer();
        }

        private void EnsureSubscribedToPlayer(IVideoPlayer player)
        {
            if (player == null) return;
            if (_subscribedPlayer == player) return;
            UnsubscribeFromPlayer();
            _subscribedPlayer = player;
            try
            {
                _subscribedPlayer.TimeChanged += Player_TimeChanged_Local;
            }
            catch { }
        }

        private void UnsubscribeFromPlayer()
        {
            if (_subscribedPlayer != null)
            {
                try
                {
                    _subscribedPlayer.TimeChanged -= Player_TimeChanged_Local;
                }
                catch { }
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
                }
            }
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
            _ownerWindow = null;
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
            if (videoPlayer != null && !videoPlayer.IsPaused())
            {
                // Pause the video when user starts seeking
                videoPlayer.TogglePause();
            }

            // Handle click anywhere on the slider track (not just thumb drag)
            Point clickPosition = e.GetPosition(SeekingSlider);
            double ratio = clickPosition.X / SeekingSlider.ActualWidth;
            ratio = Math.Max(0, Math.Min(1, ratio)); // Clamp to 0-1

            double newValue = ratio * (SeekingSlider.Maximum - SeekingSlider.Minimum) + SeekingSlider.Minimum;
            SeekingSlider.Value = newValue;
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

                // Resume playback after seeking
                if (videoPlayer.IsPaused())
                {
                    videoPlayer.TogglePause();
                }
            }

            if (videoPlayer != null)
            {
                UpdateSeekingBar(videoPlayer);
            }
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
            try
            {
                IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (monitor == IntPtr.Zero) return null;

                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(mi);
                if (GetMonitorInfo(monitor, ref mi))
                {
                    return mi.rcWork;
                }
            }
            catch { }
            return null;
        }
    }
}
