
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Cloudless.PluginBase
{
    public interface IPlugin
    {
        string Name { get; }
        string PluginVersion { get; }
        string MinAppVersion { get; }
        string Description { get; }
        List<string> SupportsFileTypes { get; }

        ImageSource Convert(byte[] bytes);
        Task<System.Windows.UIElement?> CreateView();
        Task WarmupAsync();

        void SetAnimatedSource(Image imageDisplay, BitmapImage bitmap);
        object? GetAnimationController(Image imageDisplay);

    }

}
