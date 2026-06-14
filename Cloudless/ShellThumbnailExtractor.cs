using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Cloudless
{
    internal static class ShellThumbnailExtractor
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [Flags]
        private enum SIIGBF : uint
        {
            RESIZETOFIT = 0x00,
            BIGGERSIZEOK = 0x01,
            MEMORYONLY = 0x02,
            ICONONLY = 0x04,
            THUMBNAILONLY = 0x08,
            INCACHEONLY = 0x10,
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        private interface IShellItemImageFactory
        {
            // HRESULT GetImage( SIZE size, SIIGBF flags, HBITMAP *phbm );
            void GetImage([In] SIZE size, [In] SIIGBF flags, out IntPtr phbm);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public static bool TrySaveShellThumbnail(string sourcePath, string outPath, int maxWidth, int maxHeight)
        {
            try
            {
                var size = new SIZE { cx = Math.Max(1, maxWidth), cy = Math.Max(1, maxHeight) };
                Guid iid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
                IShellItemImageFactory factory = null;
                try
                {
                    SHCreateItemFromParsingName(sourcePath, IntPtr.Zero, iid, out factory);
                }
                catch
                {
                    return false;
                }

                if (factory == null) return false;

                IntPtr hbm = IntPtr.Zero;
                try
                {
                    factory.GetImage(size, SIIGBF.RESIZETOFIT, out hbm);
                    if (hbm == IntPtr.Zero) return false;

                    var bmpSource = Imaging.CreateBitmapSourceFromHBitmap(hbm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    if (bmpSource == null) return false;

                    // Optionally resize preserving aspect if returned larger
                    BitmapSource saveSource = bmpSource;
                    // Save as PNG
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(saveSource));

                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? Path.GetTempPath());
                    using (var fs = File.Open(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        encoder.Save(fs);
                    }

                    return true;
                }
                finally
                {
                    if (hbm != IntPtr.Zero)
                        DeleteObject(hbm);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
