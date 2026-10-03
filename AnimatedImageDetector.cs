using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Cloudless
{
    public static class AnimatedImageDetector
    {
        private static readonly ConcurrentDictionary<string, Lazy<bool>> DetectionCache = new(StringComparer.OrdinalIgnoreCase);

        public static bool IsSupportedAnimatedImagePath(string? path)
        {
            string extension = Path.GetExtension(path ?? string.Empty);
            return extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        }

        public static Task<bool> IsAnimatedAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !IsSupportedAnimatedImagePath(path))
                return Task.FromResult(false);

            return Task.Run(() => IsAnimated(path));
        }

        private static bool IsAnimated(string path)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists)
                    return false;

                string cacheKey = $"{Path.GetFullPath(path)}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
                return DetectionCache.GetOrAdd(
                    cacheKey,
                    _ => new Lazy<bool>(() => DetectAnimation(path), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to inspect animated image '{path}': {ex.Message}");
                return false;
            }
        }

        private static bool DetectAnimation(string path)
        {
            try
            {
                return Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase)
                    ? IsAnimatedGif(path)
                    : IsAnimatedWebP(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to inspect animation metadata in '{path}': {ex.Message}");
                return false;
            }
        }

        private static bool IsAnimatedGif(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream);

            byte[] header = reader.ReadBytes(13);
            if (header.Length != 13 ||
                (Encoding.ASCII.GetString(header, 0, 6) != "GIF87a" && Encoding.ASCII.GetString(header, 0, 6) != "GIF89a"))
                return false;

            byte logicalScreenPacked = header[10];
            if ((logicalScreenPacked & 0x80) != 0)
            {
                long globalColorTableSize = 3L * (1 << ((logicalScreenPacked & 0x07) + 1));
                if (!SkipBytes(stream, globalColorTableSize))
                    return false;
            }

            int imageCount = 0;
            while (stream.Position < stream.Length)
            {
                int marker = stream.ReadByte();
                if (marker < 0 || marker == 0x3B)
                    return false;

                if (marker == 0x21)
                {
                    if (stream.Position >= stream.Length)
                        return false;
                    stream.ReadByte();
                    if (!SkipSubBlocks(stream))
                        return false;
                }
                else if (marker == 0x2C)
                {
                    byte[] imageDescriptor = reader.ReadBytes(9);
                    if (imageDescriptor.Length != 9)
                        return false;

                    if (++imageCount > 1)
                        return true;

                    byte imagePacked = imageDescriptor[8];
                    if ((imagePacked & 0x80) != 0)
                    {
                        long localColorTableSize = 3L * (1 << ((imagePacked & 0x07) + 1));
                        if (!SkipBytes(stream, localColorTableSize))
                            return false;
                    }

                    if (!SkipBytes(stream, 1) || !SkipSubBlocks(stream))
                        return false;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        private static bool IsAnimatedWebP(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream);

            byte[] header = reader.ReadBytes(12);
            if (header.Length != 12 ||
                Encoding.ASCII.GetString(header, 0, 4) != "RIFF" ||
                Encoding.ASCII.GetString(header, 8, 4) != "WEBP")
                return false;

            long riffEnd = Math.Min(stream.Length, 8L + BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4)));
            while (stream.Position + 8 <= riffEnd)
            {
                byte[] chunkTypeBytes = reader.ReadBytes(4);
                if (chunkTypeBytes.Length != 4)
                    return false;

                string chunkType = Encoding.ASCII.GetString(chunkTypeBytes);
                uint chunkSize = reader.ReadUInt32();
                long chunkStart = stream.Position;
                long chunkEnd = chunkStart + chunkSize;
                if (chunkEnd > riffEnd)
                    return false;

                if (chunkType == "ANIM" || chunkType == "ANMF")
                    return true;

                if (chunkType == "VP8X" && chunkSize >= 1)
                {
                    byte flags = reader.ReadByte();
                    if ((flags & 0x02) != 0)
                        return true;
                }

                stream.Position = chunkEnd + (chunkSize & 1);
            }

            return false;
        }

        private static bool SkipSubBlocks(Stream stream)
        {
            while (stream.Position < stream.Length)
            {
                int size = stream.ReadByte();
                if (size < 0)
                    return false;
                if (size == 0)
                    return true;
                if (!SkipBytes(stream, size))
                    return false;
            }

            return false;
        }

        private static bool SkipBytes(Stream stream, long count)
        {
            if (count < 0 || stream.Position + count > stream.Length)
                return false;

            stream.Seek(count, SeekOrigin.Current);
            return true;
        }
    }
}
