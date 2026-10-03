using System.IO;
using System.Diagnostics;
using System.Text.Json;

namespace Cloudless
{
    internal static class VideoPlaybackPositionStore
    {
        private const int MaxStoredPositions = 500;
        private static readonly string StorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cloudless",
            "video_positions.json");
        private static readonly Mutex StoreMutex = new(false, "Local\\CloudlessVideoPlaybackPositions");

        public static TimeSpan? GetPosition(string? mediaPath)
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
                    StoreMutex.ReleaseMutex();
            }
        }

        public static void SavePosition(string? mediaPath, TimeSpan position)
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
                positions.Remove(key);
                positions[key] = position.Ticks;

                while (positions.Count > MaxStoredPositions)
                    positions.Remove(positions.Keys.First());

                Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
                temporaryPath = StorePath + "." + Environment.ProcessId + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(positions));
                File.Move(temporaryPath, StorePath, overwrite: true);
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
                    StoreMutex.ReleaseMutex();
            }
        }

        private static bool AcquireMutex()
        {
            try
            {
                StoreMutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                return true;
            }

            return true;
        }

        private static Dictionary<string, long> ReadPositions()
        {
            var positions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(StorePath))
                return positions;

            try
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(StorePath));
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
    }
}
