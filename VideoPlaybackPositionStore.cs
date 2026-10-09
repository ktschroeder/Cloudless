using System.IO;
using System.Diagnostics;
using System.Text.Json;

namespace Cloudless
{
    internal sealed class VideoPlaybackPositionStore : IDisposable
    {
        private const int MaxStoredPositions = 500;
        private static readonly string StorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cloudless",
            "video_positions.json");
        private static readonly VideoPlaybackPositionStore Default = new(StorePath, "Local\\CloudlessVideoPlaybackPositions");

        private readonly string _storePath;
        private readonly Mutex _storeMutex;

        internal VideoPlaybackPositionStore(string storePath, string mutexName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
            _storePath = Path.GetFullPath(storePath);
            _storeMutex = new Mutex(false, mutexName);
        }

        public static TimeSpan? GetPosition(string? mediaPath)
        {
            return Default.ReadPosition(mediaPath);
        }

        internal TimeSpan? ReadPosition(string? mediaPath)
        {
            if (string.IsNullOrWhiteSpace(mediaPath))
                return null;

            bool ownsMutex = false;
            try
            {
                ownsMutex = AcquireMutex();
                if (!ownsMutex)
                    return null;

                string key = Path.GetFullPath(mediaPath);
                var positions = ReadPositions();
                return positions.TryGetValue(key, out long ticks) && ticks >= 0
                    ? TimeSpan.FromTicks(ticks)
                    : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read saved video position: {ex.Message}");
                return null;
            }
            finally
            {
                if (ownsMutex)
                    _storeMutex.ReleaseMutex();
            }
        }

        public static void SavePosition(string? mediaPath, TimeSpan position)
        {
            Default.WritePosition(mediaPath, position);
        }

        internal void WritePosition(string? mediaPath, TimeSpan position)
        {
            if (string.IsNullOrWhiteSpace(mediaPath) || position < TimeSpan.Zero)
                return;

            bool ownsMutex = false;
            string? temporaryPath = null;
            try
            {
                ownsMutex = AcquireMutex();
                if (!ownsMutex)
                    return;

                string key = Path.GetFullPath(mediaPath);
                var positions = ReadPositions();
                if (positions.Remove(key))
                    positions = new Dictionary<string, long>(positions, StringComparer.OrdinalIgnoreCase);
                positions[key] = position.Ticks;

                while (positions.Count > MaxStoredPositions)
                    positions.Remove(positions.Keys.First());

                Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
                temporaryPath = _storePath + "." + Environment.ProcessId + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(positions));
                File.Move(temporaryPath, _storePath, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save video position: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (temporaryPath != null && File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to remove temporary video position file: {ex.Message}");
                }

                if (ownsMutex)
                    _storeMutex.ReleaseMutex();
            }
        }

        private bool AcquireMutex()
        {
            try
            {
                _storeMutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                return true;
            }

            return true;
        }

        private Dictionary<string, long> ReadPositions()
        {
            var positions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(_storePath))
                return positions;

            try
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(_storePath));
                if (loaded != null)
                {
                    foreach (var entry in loaded)
                    {
                        if (entry.Value >= 0)
                            positions[entry.Key] = entry.Value;
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Saved video positions could not be parsed: {ex.Message}");
            }

            return positions;
        }

        public void Dispose()
        {
            _storeMutex.Dispose();
        }
    }
}
