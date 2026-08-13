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
        private static bool _slideshowShuffle = false;
        // lots per page value for smart randomness (pageValue -> lots)
        private static Dictionary<int, int>? _slideshowLots = null;
        // last page value visited prior to the current one (for exclusion rules)
        private static int _slideshowPreviousPage = -1;
        private static readonly Random _rng = new Random();

        public static event Action? SlideshowStarted;
        public static event Action? SlideshowStopped;
        public static bool IsRunning => _slideshowTimer != null && _slideshowTimer.IsEnabled;
        public static double CurrentIntervalSeconds => _slideshowIntervalSeconds;
        public static List<int>? CurrentPages => _slideshowPages;
        // The page selected by the last timer tick (if any). Manager sets this when it chooses the next page.
        public static int? SelectedPage { get; private set; } = null;

        public static void Initialize(double intervalSeconds, List<int> activePages, int startingPageIndex, Dispatcher dispatcher, Action onTick, bool shuffle = false)
        {
            // Stop any existing slideshow
            Stop();

            if (activePages.Count == 0)
                return;

            _slideshowIntervalSeconds = intervalSeconds;
            _slideshowPages = new List<int>(activePages);
            _slideshowCurrentPageIndex = startingPageIndex;
            _onSlideshowTick = onTick;
            _slideshowShuffle = shuffle;
            SelectedPage = null;

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
            // clear shuffle-related state
            _slideshowShuffle = false;
            _slideshowLots = null;
            _slideshowPreviousPage = -1;
            SelectedPage = null;
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

            if (!_slideshowShuffle)
            {
                // Advance to next page (round-robin)
                int currentPage = _slideshowPages[_slideshowCurrentPageIndex];
                _slideshowPreviousPage = currentPage;
                _slideshowCurrentPageIndex = (_slideshowCurrentPageIndex + 1) % _slideshowPages.Count;
                int chosenPage = _slideshowPages[_slideshowCurrentPageIndex];
                SelectedPage = chosenPage;
                _onSlideshowTick?.Invoke();
                return;
            }

            // Shuffle mode with smart lots
            try
            {
                int count = _slideshowPages.Count;
                int currentPage = _slideshowPages[_slideshowCurrentPageIndex];

                // ensure lots dictionary exists and contains all pages
                if (_slideshowLots == null)
                    _slideshowLots = new Dictionary<int, int>();
                foreach (var p in _slideshowPages)
                {
                    if (!_slideshowLots.ContainsKey(p))
                        _slideshowLots[p] = 1;
                }

                // Build candidate list: exclude current page. If >=4 pages, also exclude previous page.
                var candidates = new List<int>();
                foreach (var p in _slideshowPages)
                {
                    if (p == currentPage) continue;
                    if (_slideshowPages.Count >= 4 && _slideshowPreviousPage != -1 && p == _slideshowPreviousPage) continue;
                    candidates.Add(p);
                }

                // If no candidates (can happen if pages<4 and only other page excluded), fall back to all except current
                if (candidates.Count == 0)
                {
                    foreach (var p in _slideshowPages)
                    {
                        if (p == currentPage) continue;
                        candidates.Add(p);
                    }
                }

                int totalLots = 0;
                foreach (var c in candidates)
                {
                    if (_slideshowLots.TryGetValue(c, out int l))
                        totalLots += Math.Max(0, l);
                }

                int chosenPage = -1;
                if (totalLots <= 0)
                {
                    // fallback to uniform random among candidates
                    if (candidates.Count > 0)
                    {
                        int idx = _rng.Next(0, candidates.Count);
                        chosenPage = candidates[idx];
                    }
                }
                else
                {
                    int r = _rng.Next(0, totalLots);
                    int acc = 0;
                    foreach (var c in candidates)
                    {
                        int l = _slideshowLots.TryGetValue(c, out int lv) ? Math.Max(0, lv) : 0;
                        acc += l;
                        if (r < acc)
                        {
                            chosenPage = c;
                            break;
                        }
                    }
                }

                if (chosenPage == -1)
                {
                    // no valid candidate, just advance round-robin
                    _slideshowPreviousPage = currentPage;
                    _slideshowCurrentPageIndex = (_slideshowCurrentPageIndex + 1) % _slideshowPages.Count;
                    int cp = _slideshowPages[_slideshowCurrentPageIndex];
                    SelectedPage = cp;
                    _onSlideshowTick?.Invoke();
                    return;
                }

                // Update lots: remove all lots from chosen page, add one lot to all other pages (excluding chosen)
                foreach (var p in _slideshowPages)
                {
                    if (p == chosenPage)
                        _slideshowLots[p] = 0;
                    else
                        _slideshowLots[p] = (_slideshowLots.TryGetValue(p, out int lv) ? lv : 0) + 1;
                }

                // Update previous/current indices
                _slideshowPreviousPage = currentPage;
                int newIndex = _slideshowPages.IndexOf(chosenPage);
                if (newIndex >= 0)
                    _slideshowCurrentPageIndex = newIndex;

                // Expose the chosen page to callers and invoke tick handler
                SelectedPage = chosenPage;
                _onSlideshowTick?.Invoke();
            }
            catch
            {
                // on any error, fallback to simple advance
                _slideshowCurrentPageIndex = (_slideshowCurrentPageIndex + 1) % _slideshowPages.Count;
                _onSlideshowTick?.Invoke();
            }
        }
    }
}
