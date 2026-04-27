
namespace Cloudless.PluginBase
{
    public interface IPlugin
    {
        string Name { get; }
        string Description { get; }

        int Execute();
    }

}
