using System.Windows.Threading;

namespace Cloudless
{
    /// <summary>
    /// Manages slideshow state globally across all window instances.
    /// Ensures that slideshow operations are coordinated across the entire application.
    /// </summary>
    public static class SlideshowManager
    {
        private static DispatcherTimer? _slideshowTimer = null;
        private static double _slideshowIntervalSeconds = 0;
        private static List<int>? _slideshowPages = null;
        private static int _slideshowCurrentPageIndex = 0;
        private static Action? _onSlideshowTick = null;
        private static Action? _onSlideshowStopped = null;

        public static event Action? SlideshowStarted;
        public static event Action? SlideshowStopped;
        public static bool IsRunning => _slideshowTimer != null && _slideshowTimer.IsEnabled;
        public static double CurrentIntervalSeconds => _slideshowIntervalSeconds;
        public static List<int>? CurrentPages => _slideshowPages;

        public static void Initialize(double intervalSeconds, List<int> activePages, int startingPageIndex, Dispatcher dispatcher, Action onTick)
        {
            // Stop any existing slideshow
            Stop();

            if (activePages.Count == 0)
                return;

            _slideshowIntervalSeconds = intervalSeconds;
            _slideshowPages = new List<int>(activePages);
            _slideshowCurrentPageIndex = startingPageIndex;
            _onSlideshowTick = onTick;

            _slideshowTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(intervalSeconds)
            };

            _slideshowTimer.Tick += (sender, e) =>
            {
                OnTimerTick();
            };

            _slideshowTimer.Start();

            // Raise event to notify all windows
            SlideshowStarted?.Invoke();
        }

        /// <summary>
        /// Stops the currently running slideshow.
        /// Can be called from any window instance.
        /// </summary>
        public static void Stop()
        {
            if (_slideshowTimer != null)
            {
                _slideshowTimer.Stop();
                _slideshowTimer = null;
            }

            _slideshowPages = null;
            _slideshowCurrentPageIndex = 0;
            _onSlideshowTick = null;

            if (_slideshowIntervalSeconds > 0)
            {
                // Raise event to notify all windows
                SlideshowStopped?.Invoke();
            }

            _slideshowIntervalSeconds = 0;
        }

        /// <summary>
        /// Internal method called on each timer tick.
        /// Advances to the next page in the slideshow.
        /// </summary>
        private static void OnTimerTick()
        {
            if (_slideshowPages == null || _slideshowPages.Count == 0)
            {
                Stop();
                return;
            }

            // Advance to next page
            _slideshowCurrentPageIndex = (_slideshowCurrentPageIndex + 1) % _slideshowPages.Count;

            // Call the tick handler (which will be MainWindow.SlideshowTimer_Tick)
            _onSlideshowTick?.Invoke();
        }
    }
}
