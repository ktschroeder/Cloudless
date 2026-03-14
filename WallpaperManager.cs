using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Win32;

namespace Cloudless
{
    public static class WallpaperHelper
    {
        private const int SPI_SETDESKWALLPAPER = 0x0014;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(
            int uAction,
            int uParam,
            string lpvParam,
            int fuWinIni);

        public static void SetWallpaper(Bitmap bitmap)
        {
            string path = SaveBitmapToTempFile(bitmap);

            SetWallpaperStyleFill();

            bool result = SystemParametersInfo(
                SPI_SETDESKWALLPAPER,
                0,
                path,
                SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

            if (!result)
                throw new Exception("Failed to set wallpaper.");
        }

        public static void SetWallpaper(string path)
        {
            var bitmap = new Bitmap(path);
            SetWallpaper(bitmap);
        }

        private static string SaveBitmapToTempFile(Bitmap bitmap)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "CloudlessWallpapers");

            if (!Directory.Exists(tempDir))
                Directory.CreateDirectory(tempDir);

            string filePath = Path.Combine(
                tempDir,
                $"wallpaper_{Guid.NewGuid()}.png");

            bitmap.Save(filePath, ImageFormat.Png);

            return filePath;
        }

        private static void SetWallpaperStyleFill()
        {
            Registry.SetValue(
                @"HKEY_CURRENT_USER\Control Panel\Desktop",
                "WallpaperStyle",
                "10"); // Fill

            Registry.SetValue(
                @"HKEY_CURRENT_USER\Control Panel\Desktop",
                "TileWallpaper",
                "0");
        }
    }
}