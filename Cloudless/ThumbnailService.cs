using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Cloudless
{
    public static class ThumbnailService
    {
        private static readonly string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cloudless", "thumbnails");
        private static readonly SemaphoreSlim concurrency = new SemaphoreSlim(2);

        static ThumbnailService()
        {
            try { Directory.CreateDirectory(cacheDir); } catch { }
        }

        public static async Task<BitmapSource?> GetThumbnailAsync(string filePath, int maxWidth, int maxHeight)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            try
            {
                var fi = new FileInfo(filePath);
                if (!fi.Exists) return null;

                string key = filePath.ToLowerInvariant() + "|" + fi.LastWriteTimeUtc.Ticks + $"|{maxWidth}x{maxHeight}";
                string hash;
                using (var sha = SHA1.Create())
                {
                    var data = Encoding.UTF8.GetBytes(key);
                    hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
                }

                string outPath = Path.Combine(cacheDir, hash + ".png");
                if (File.Exists(outPath))
                {
                    try
                    {
                        var bi = new BitmapImage();
                        using (var fs = File.OpenRead(outPath))
                        {
                            bi.BeginInit();
                            bi.CacheOption = BitmapCacheOption.OnLoad;
                            bi.StreamSource = fs;
                            bi.EndInit();
                            bi.Freeze();
                        }
                        return bi;
                    }
                    catch
                    {
                        try { File.Delete(outPath); } catch { }
                    }
                }

                await concurrency.WaitAsync();
                try
                {
                    // Try shell extractor
                    bool ok = await Task.Run(() => ShellThumbnailExtractor.TrySaveShellThumbnail(filePath, outPath, maxWidth, maxHeight));
                    if (!ok)
                        return null;

                    var bi = new BitmapImage();
                    using (var fs = File.OpenRead(outPath))
                    {
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = fs;
                        bi.EndInit();
                        bi.Freeze();
                    }
                    return bi;
                }
                finally
                {
                    concurrency.Release();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
