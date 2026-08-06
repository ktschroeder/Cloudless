using Cloudless.PluginBase;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cloudless.PluginBase;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        private bool _isSeekingBarVisible = false;
        private bool _isUserSeeking = false;

        private void InitializeSeekingBar()
        {
            // Handle clicking anywhere on the slider track to seek
            SeekingSlider.PreviewMouseDown += SeekingSlider_PreviewMouseDown;
            SeekingSlider.PreviewMouseUp += SeekingSlider_PreviewMouseUp;
        }

        public void ToggleSeekingBarVisibility()
        {
            _isSeekingBarVisible = !_isSeekingBarVisible;
            SeekingBarContainer.Visibility = _isSeekingBarVisible ? Visibility.Visible : Visibility.Collapsed;

            if (_isSeekingBarVisible)
            {
                // Hook into the video player's time changed event if available
                AttachToVideoPlayerEvents();
                UpdateSeekingBar();
            }
            else
            {
                DetachFromVideoPlayerEvents();

                // Resume video if it was paused for seeking
                if (_isUserSeeking)
                {
                    _isUserSeeking = false;
                    var videoPlayer = VideoHost.Content as IVideoPlayer;
                    if (videoPlayer != null && videoPlayer.IsPaused())
                    {
                        videoPlayer.TogglePause();
                    }
                }
            }
        }

        private void AttachToVideoPlayerEvents()
        {
            try
            {
                if (VideoHost.Content is IVideoPlayer videoPlayer)
                {
                    videoPlayer.TimeChanged += VideoPlayer_TimeChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error attaching to video player events: {ex.Message}");
            }
        }

        private void DetachFromVideoPlayerEvents()
        {
            try
            {
                if (VideoHost.Content is IVideoPlayer videoPlayer)
                {
                    videoPlayer.TimeChanged -= VideoPlayer_TimeChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error detaching from video player events: {ex.Message}");
            }
        }

        private void VideoPlayer_TimeChanged(object? sender, VideoTimeChangedEventArgs e)
        {
            // This event fires from a background thread, so we need to marshal to the UI thread
            if (_isUserSeeking) return;

            Dispatcher.Invoke(() =>
            {
                try
                {
                    var videoPlayer = VideoHost.Content as IVideoPlayer;
                    if (videoPlayer == null) return;

                    TimeSpan duration = videoPlayer.GetDuration();

                    if (duration > TimeSpan.Zero)
                    {
                        SeekingSlider.Maximum = (int)duration.TotalMilliseconds;
                        SeekingSlider.Value = (int)e.TimeMilliseconds;

                        TimeSpan position = TimeSpan.FromMilliseconds(e.TimeMilliseconds);
                        CurrentTimeText.Text = FormatTime(position);
                        DurationText.Text = FormatTime(duration);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in VideoPlayer_TimeChanged: {ex.Message}");
                }
            });
        }

        private void UpdateSeekingBar()
        {
            var videoPlayer = VideoHost.Content as IVideoPlayer;
            if (videoPlayer == null) return;

            try
            {
                TimeSpan duration = videoPlayer.GetDuration();
                TimeSpan position = videoPlayer.GetPosition();

                if (duration <= TimeSpan.Zero)
                {
                    SeekingSlider.Maximum = 1000;
                    SeekingSlider.Value = 0;
                }
                else
                {
                    SeekingSlider.Maximum = (int)duration.TotalMilliseconds;
                    SeekingSlider.Value = (int)position.TotalMilliseconds;
                }

                CurrentTimeText.Text = FormatTime(position);
                DurationText.Text = FormatTime(duration);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating seeking bar: {ex.Message}");
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

            var videoPlayer = VideoHost.Content as IVideoPlayer;
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

            var videoPlayer = VideoHost.Content as IVideoPlayer;
            if (videoPlayer != null)
            {
                try
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error seeking: {ex.Message}");
                }
            }

            UpdateSeekingBar();
        }
    }
}
