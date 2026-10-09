using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Cloudless
{
    /// <summary>
    /// Lightweight anticipatory image preloader.
    /// - Reads file bytes on threadpool
    /// - Creates frozen BitmapImage on UI thread
    /// - Caches a small window of adjacent images (previous/next)
    /// Conservative: does not preload video files; only loads image file types (including GIF/WEBP/PNG/JPEG).
    /// </summary>
    public sealed class PreloadManager : IDisposable
    {
        private readonly Dispatcher _uiDispatcher;
        private readonly ConcurrentDictionary<string, BitmapImage> _cache = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _cts;
        private readonly object _sync = new();
        private bool _disposed = false;

        private readonly int _preloadNext = 5;
        private readonly int _preloadPrev = 2;

        public PreloadManager(Dispatcher uiDispatcher)
        {
            _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        }

        internal static List<int> GetPreloadIndices(int currentIndex, int itemCount, int preloadNext = 5, int preloadPrev = 2)
        {
            if (itemCount <= 0 || currentIndex < 0 || currentIndex >= itemCount)
                return new List<int>();

            var indices = new List<int> { currentIndex };
            for (int i = 1; i <= preloadNext; i++)
            {
                int index = currentIndex + i;
                if (index < itemCount)
                    indices.Add(index);
            }
            for (int i = 1; i <= preloadPrev; i++)
            {
                int index = currentIndex - i;
                if (index >= 0)
                    indices.Add(index);
            }

            return indices;
        }

        internal static List<int> GetCacheRetentionIndices(int currentIndex, int itemCount, int radius = 10)
        {
            if (itemCount <= 0 || currentIndex < 0 || currentIndex >= itemCount || radius < 0)
                return new List<int>();

            int startIndex = Math.Max(0, currentIndex - radius);
            int endIndex = Math.Min(itemCount - 1, currentIndex + radius);
            var indices = new List<int>(endIndex - startIndex + 1);
            for (int index = startIndex; index <= endIndex; index++)
                indices.Add(index);

            return indices;
        }

        /// <summary>
        /// Try get a cached BitmapImage (frozen) for the given path.
        /// </summary>
        public bool TryGet(string path, out BitmapImage? image)
        {
            if (path == null || !Cloudless.Properties.Settings.Default.PreloadImages) { image = null; return false; }
            return _cache.TryGetValue(path, out image);
        }

        /// <summary>
        /// Kick off preloads around the given index for the provided file list.
        /// Cancels any previously scheduled preloads.
        /// </summary>
        public void PreloadWindow(int currentIndex, string[]? fileList)
        {
            if (_disposed) return;

            if (!Cloudless.Properties.Settings.Default.PreloadImages) return;

            lock (_sync)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
            }

            if (fileList == null || fileList.Length == 0)
                return;

            var token = _cts.Token;

            var indices = GetPreloadIndices(currentIndex, fileList.Length, _preloadNext, _preloadPrev);

            // Launch background workers for each index
            foreach (int idx in indices)
            {
                string path = fileList[idx];
                // Skip if cached already
                if (_cache.ContainsKey(path)) continue;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (token.IsCancellationRequested) return;

                        string ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
                        FileType? type = FileTypeManager.GetFileTypeByExtension(ext);
                        if (type == null || type.IsVideo) 
                            return;

                        // Read bytes on threadpool
                        byte[] bytes;
                        try
                        {
                            bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
                        }
                        catch
                        {
                            return;
                        }

                        if (token.IsCancellationRequested) return;

                        // Create BitmapImage on UI thread and Freeze it
                        await _uiDispatcher.InvokeAsync(() =>
                        {
                            if (token.IsCancellationRequested) return;

                            using var ms = new MemoryStream(bytes);
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                            bitmap.StreamSource = ms;
                            bitmap.EndInit();
                            bitmap.Freeze(); // make it cross-thread accessible
                            // Store into cache (if not already stored due to race)
                            _cache.TryAdd(path, bitmap);
                        }, DispatcherPriority.Background);
                    }
                    catch (OperationCanceledException) { }
                }, token);
            }

            // Prune cache entries that are far away from current window asynchronously
            _ = Task.Run(() =>
            {
                var keepPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (int index in GetCacheRetentionIndices(currentIndex, fileList.Length))
                    keepPaths.Add(fileList[index]);

                // remove keys not in keepPaths
                foreach (var key in _cache.Keys)
                {
                    if (!keepPaths.Contains(key))
                    {
                        if (_cache.TryRemove(key, out var removed))
                        {
                            // nothing special to dispose for frozen BitmapImage
                            // but clear reference to allow GC
                            removed = null;
                        }
                    }
                }
            });
        }

        public List<string> GetPreloadCacheKeys()
        {
            return _cache.Keys.Select(k => Path.GetFileName(k)).ToList();
        }

        /// <summary>
        /// Clear all cached preloads.
        /// </summary>
        public void Clear()
        {
            lock (_sync)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
            }

            _cache.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }
    }
}