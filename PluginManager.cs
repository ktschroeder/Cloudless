using Cloudless.PluginBase;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Cloudless
{
    public static class PluginManager
    {
        private static IEnumerable<IPlugin> GetPlugins()  // TODO make useful
        {
            string[] pluginPaths = new string[]
                {
                    // Paths to plugins to load.
                    @"Cloudless\Cloudless.WebpPlugin\bin\Debug\net8.0-windows\Cloudless.WebpPlugin.dll"
                };

            IEnumerable<IPlugin> plugins = pluginPaths.SelectMany(pluginPath =>
            {
                Assembly pluginAssembly = PluginManager.LoadPlugin(pluginPath);
                return PluginManager.CreateCommands(pluginAssembly);
            }).ToList();

            return plugins;
        }

        public static IPlugin? GetPluginForFiletype(string fileType)
        {
            IEnumerable<IPlugin> plugins = GetPlugins(); 
            var plugin = plugins.FirstOrDefault(p => p.SupportsFileType.Equals(fileType, StringComparison.OrdinalIgnoreCase));
            return plugin;
        }

        public static Assembly LoadPlugin(string relativePath)
        {
            // Navigate up to the solution root
            string root = Path.GetFullPath(
                Path.Combine(typeof(MainWindow).Assembly.Location, "..", "..", "..", "..", ".."));

            string pluginLocation = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
            Console.WriteLine($"Loading commands from: {pluginLocation}");
            PluginLoadContext loadContext = new(pluginLocation);
            //return loadContext.LoadFromAssemblyName(new(Path.GetFileNameWithoutExtension(pluginLocation)));
            return loadContext.LoadFromAssemblyName(AssemblyName.GetAssemblyName(pluginLocation));
        }

        public static IEnumerable<IPlugin> CreateCommands(Assembly assembly)
        {
            int count = 0;

            //foreach (var type in assembly.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t)))
            //foreach (var type in assembly.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t)))
            //{
            //    if (Activator.CreateInstance(type) is IPlugin result)
            //    {
            //        count++;
            //        yield return result;
            //    }
            //}

            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(type))
                {
                    IPlugin result = Activator.CreateInstance(type) as IPlugin;
                    if (result != null)
                    {
                        count++;
                        yield return result;
                    }
                }
            }


            if (count == 0)
            {
                string availableTypes = string.Join(",", assembly.GetTypes().Select(t => t.FullName));
                throw new ApplicationException(
                    $"Can't find any type which implements IPlugin in {assembly} from {assembly.Location}.\n" +
                    $"Available types: {availableTypes}");
            }
        }
    }
}