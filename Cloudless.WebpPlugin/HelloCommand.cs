using Cloudless.PluginBase;

namespace Cloudless.WebpPlugin
{
    public class HelloCommand : IPlugin
    {
        public string Name { get => "hello"; }
        public string Description { get => "Displays hello message."; }

        public int Execute()
        {
            Console.WriteLine("Hello !!!");
            return 0;
        }
    }
}
