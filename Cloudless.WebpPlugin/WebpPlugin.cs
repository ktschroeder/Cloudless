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
        public string Description { get => "Converts a WEBP image to bitmap using an external WEBP library"; }
        public string SupportsFileType { get => "webp"; }

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

        public UIElement? CreateView()
        {
            return null;
        }
    }
}
