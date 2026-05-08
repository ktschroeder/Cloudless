using Cloudless.PluginBase;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WebP.Net;

namespace Cloudless.WebpPlugin
{
    public class WebpPlugin : IPlugin
    {
        public string Name { get => "WEBP Plugin"; }
        public string PluginVersion { get => "1.0.3"; }
        public string MinAppVersion { get => "0.7.0"; }
        public string Description { get => "Converts a WEBP image to bitmap using an external WEBP library"; }
        public List<string> SupportsFileTypes { get => new List<string> { "webp" }; }

        public ImageSource Convert(byte[] bytes)
        {
            using var webp = new WebPObject(bytes);
            var webpImage = webp.GetImage();
            BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                ((Bitmap)webpImage).GetHbitmap(),
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions()
            );
            return bitmapSource;
        }

        public Task<UIElement?> CreateView()
        {
            return null;
        }

        public async Task WarmupAsync()
        {
            return;
        }
    }
}
