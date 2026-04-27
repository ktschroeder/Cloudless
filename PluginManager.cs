using Cloudless.PluginBase;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Cloudless
{
    // Plugin framework adapted from https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support
    public static class PluginManager
    {
        private static IEnumerable<IPlugin> GetPlugins()  // TODO make useful
        {
            string[] pluginPaths = new string[]
                {
                    // Paths to plugins to load.
                    //@"Cloudless\Cloudless.WebpPlugin\bin\Debug\net8.0-windows\Cloudless.WebpPlugin.dll",
                    //@"Cloudless\Cloudless.WebmPlugin\bin\Debug\net8.0-windows\Cloudless.WebmPlugin.dll",
                    Path.Join(MainWindow.pluginsFilesPath, "webp", "Cloudless.WebpPlugin.dll"),
                    Path.Join(MainWindow.pluginsFilesPath, "webm", "Cloudless.WebmPlugin.dll")
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
            try
            {
                IEnumerable<IPlugin> plugins = GetPlugins();
                var plugin = plugins.FirstOrDefault(p => p.SupportsFileType.Equals(fileType, StringComparison.OrdinalIgnoreCase));
                return plugin;
            }
            catch (Exception e)
            {
                return null;  // TODO probably should log this exception in system messages in case of some issue other than missing plugin
            }
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

        public static async Task<bool> InstallPluginAsync(
        string pluginName,
        string downloadUrl,
        IProgress<string>? progress = null)
        {
            try
            {
                var pluginsDir = MainWindow.pluginsFilesPath;
                var pluginDir = Path.Combine(pluginsDir, pluginName.ToLower());

                Directory.CreateDirectory(pluginsDir);

                var tempZipPath = Path.Combine(Path.GetTempPath(), $"{pluginName}.zip");
                var tempExtractDir = Path.Combine(Path.GetTempPath(), $"{pluginName}_extract");

                progress?.Report("Downloading plugin...");

                using (var client = new HttpClient())
                {
                    var data = await client.GetByteArrayAsync(downloadUrl);
                    await File.WriteAllBytesAsync(tempZipPath, data);
                }

                progress?.Report("Extracting plugin...");

                if (Directory.Exists(tempExtractDir))
                    Directory.Delete(tempExtractDir, true);

                ZipFile.ExtractToDirectory(tempZipPath, tempExtractDir);

                // Optional: ensure expected folder exists
                var extractedPluginDir = Path.Combine(tempExtractDir, pluginName);
                if (!Directory.Exists(extractedPluginDir))
                    throw new Exception("Invalid plugin structure.");

                progress?.Report("Installing plugin...");

                if (Directory.Exists(pluginDir))
                    Directory.Delete(pluginDir, true);

                Directory.Move(extractedPluginDir, pluginDir);

                // Cleanup
                File.Delete(tempZipPath);
                Directory.Delete(tempExtractDir, true);

                progress?.Report("Plugin installed successfully.");

                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"Failed: {ex.Message}");
                return false;
            }
        }
    }
}