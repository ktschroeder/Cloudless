using Cloudless.PluginBase;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;

namespace Cloudless.VlcPlugin
{
    public class VlcPlugin : IPlugin
    {
        public string Name { get => "VLC Plugin"; }
        public string PluginVersion { get => "1.0.6"; }
        public string MinAppVersion { get => "0.9.0"; }
        public string Description { get => "Prepares a WPF view for a WEBM/MKV/MP4 video using external VLC libraries"; }
        public List<string> SupportsFileTypes { get => new List<string> { "webm", "mkv", "mp4" }; }

        public ImageSource Convert(byte[] bytes)
        {
            return null;
        }

        public async Task<UIElement?> CreateView()
        {
            var wpc = new VlcVideoPlayerControl();
            await wpc.Initialize();
            return wpc;
        }

        public async Task WarmupAsync()
        {
            await LibVlcProvider.WarmupAsync();
        }

        public void SetAnimatedSource(Image ImageDisplay, BitmapImage bitmap)
        {
            throw new NotImplementedException();
        }

        public object? GetAnimationController(Image imageDisplay)
        {
            throw new NotImplementedException(); 
        }
    }
}
