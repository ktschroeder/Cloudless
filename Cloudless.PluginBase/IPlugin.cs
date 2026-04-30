
using System.Windows.Media;

namespace Cloudless.PluginBase
{
    public interface IPlugin
    {
        string Name { get; }
        string Description { get; }
        List<string> SupportsFileTypes { get; }

        ImageSource Convert(byte[] bytes);
        Task<System.Windows.UIElement?> CreateView();
        Task WarmupAsync();
        
    }

}
