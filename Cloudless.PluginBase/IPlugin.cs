
using System.Windows.Media;

namespace Cloudless.PluginBase
{
    public interface IPlugin
    {
        string Name { get; }
        string Description { get; }
        string SupportsFileType { get; }

        ImageSource Convert(byte[] bytes);
        System.Windows.UIElement? CreateView();
    }

}
