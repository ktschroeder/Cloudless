using Cloudless.PluginBase;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Cloudless.WebmPlugin
{
    public class WebmPlugin : IPlugin
    {
        public string Name { get => "WEBM Plugin"; }
        public string Description { get => "Prepares a WPF view for a WEBM using external VLC libraries"; }
        public string SupportsFileType { get => "webm"; }

        public ImageSource Convert(byte[] bytes)
        {
            return null;
        }

        public async Task<UIElement?> CreateView()
        {
            var wpc = new WebmPlayerControl();
            await wpc.Initialize();
            return wpc;
        }

        public async Task WarmupAsync()
        {
            await LibVlcProvider.WarmupAsync();
        }
    }
}
