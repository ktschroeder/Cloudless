using Cloudless.PluginBase;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Cloudless
{
    // Plugin framework adapted from https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support
    public static class PluginManager
    {
        public static void InitializePlugins()
        {
            IEnumerable<IPlugin> plugins = GetPlugins();
            foreach (IPlugin plugin in plugins)
            {
                Task.Run(() => plugin.WarmupAsync());
            }
        }

        private static IEnumerable<IPlugin> GetPlugins()  // TODO make useful
        {
            if (MainWindow.LOCAL_DEV)
            {
                string[] pluginPaths = new string[]
                {
                    @"Cloudless\Cloudless.AnimatedWebpPlugin\bin\Debug\net8.0-windows\Cloudless.AnimatedWebpPlugin.dll",
                    @"Cloudless\Cloudless.VlcPlugin\bin\Debug\net8.0-windows\Cloudless.VlcPlugin.dll"
                };

                IEnumerable<IPlugin> plugins = pluginPaths.SelectMany(pluginPath =>
                {
                    Assembly pluginAssembly = PluginManager.LoadPlugin(pluginPath);
                    return PluginManager.CreateCommands(pluginAssembly);
                }).ToList();

                return plugins;
            }
            else
            {
                var pluginRoots = new[]
                {
                    ("webp", "Cloudless.WebpPlugin.dll"),
                    ("vlc", "Cloudless.VlcPlugin.dll")
                };

                var pluginPaths = pluginRoots
                    .Select(p =>
                        GetLatestPluginAssemblyPath(
                            Path.Combine(MainWindow.pluginsFilesPath, p.Item1),
                            p.Item2))
                    .Where(p => p != null)!;

                return pluginPaths.SelectMany(pluginPath =>
                {
                    var assembly = LoadPlugin(pluginPath);
                    return CreateCommands(assembly);
                }).ToList();
            }
                
        }

        private static string? GetLatestPluginAssemblyPath(string pluginRootDir, string dllName)
        {
            if (!Directory.Exists(pluginRootDir))
                return null;

            var versionDirs = Directory.GetDirectories(pluginRootDir);

            var best = versionDirs
                .Select(dir => new
                {
                    Path = dir,
                    Version = TryParseVersion(Path.GetFileName(dir))
                })
                .Where(x => x.Version != null)
                .OrderByDescending(x => x.Version)
                .FirstOrDefault();

            if (best == null)
                return null;

            var dllPath = Path.Combine(best.Path, dllName);
            return File.Exists(dllPath) ? dllPath : null;
        }

        private static Version? TryParseVersion(string folderName)
        {
            // supports "v1.2.3" or "1.2.3"
            folderName = folderName.TrimStart('v', 'V');

            return Version.TryParse(folderName, out var v) ? v : null;
        }

        public static IPlugin? GetPluginForFiletype(string fileType)
        {
            try
            {
                IEnumerable<IPlugin> plugins = GetPlugins();
                var plugin = plugins.FirstOrDefault(p => p.SupportsFileTypes.Contains(fileType, StringComparer.OrdinalIgnoreCase));
                return plugin;
            }
            catch (Exception e)
            {
                return null;  // TODO probably should log this exception in system messages in case of some issue other than missing plugin
            }
        }

        public static IPlugin? GetPluginByName(string pluginName)
        {
            try
            {
                IEnumerable<IPlugin> plugins = GetPlugins();
                var plugin = plugins.FirstOrDefault(p => string.Equals(p.Name, pluginName, StringComparison.OrdinalIgnoreCase));
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

        // TODO use me
        private static void CleanupOldVersions(string pluginRootDir, int keepLatest = 2)
        {
            var dirs = Directory.GetDirectories(pluginRootDir)
                .Select(d => new
                {
                    Path = d,
                    Version = TryParseVersion(Path.GetFileName(d))
                })
                .Where(x => x.Version != null)
                .OrderByDescending(x => x.Version)
                .ToList();

            foreach (var old in dirs.Skip(keepLatest))
            {
                try
                {
                    Directory.Delete(old.Path, true);
                }
                catch
                {
                    // still in use, so ignore
                }
            }
        }

        public static async Task<bool> InstallPluginAsync(
        string pluginName,
        string downloadUrl,
        IProgress<string>? progress = null,
        bool continuingInstallInParts = false)
        {
            try
            {
                var pluginsDir = MainWindow.pluginsFilesPath;
                //var pluginDir = Path.Combine(pluginsDir, pluginName.ToLower());
                var pluginRootDir = Path.Combine(pluginsDir, pluginName.ToLower());

                

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

                var manifestPath = Path.Combine(extractedPluginDir, "manifest.json");
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

                string version = manifest.PluginVersion;
                var versionDir = Path.Combine(pluginRootDir, version);

                var existingPlugin = GetPluginByName(manifest.Name);
                if (existingPlugin != null)
                {
                    Version existingVersion = Version.Parse(existingPlugin.PluginVersion);
                    Version newVersion = Version.Parse(manifest.PluginVersion);
                    if (existingVersion >= newVersion)
                    {
                        progress?.Report("Did not install: This plugin is already up-to-date.");
                        return false;
                    }
                }

                Directory.CreateDirectory(versionDir);

                progress?.Report("Installing plugin...");

                try
                {
                    if (Directory.Exists(versionDir) && !continuingInstallInParts)
                        Directory.Delete(versionDir, true);
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new Exception($"Access error: Failed to clear existing plugin directory: {ex.Message}");
                }

                MoveAndMerge(extractedPluginDir, versionDir);  // used instead of Directory.Move to permit merge behavior for partial installs.

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

        private static void MoveAndMerge(string sourcePath, string destPath)
        {
            Directory.CreateDirectory(destPath);

            // Move all files
            foreach (var file in Directory.GetFiles(sourcePath))
            {
                string destFile = Path.Combine(destPath, Path.GetFileName(file));
                File.Move(file, destFile, overwrite: true);
            }

            // Recursively move subdirectories
            foreach (var dir in Directory.GetDirectories(sourcePath))
            {
                string destDir = Path.Combine(destPath, Path.GetFileName(dir));
                MoveAndMerge(dir, destDir);
            }

            // Delete source after it's empty
            Directory.Delete(sourcePath, true);
        }
    }

    public class PluginManifest
    {
        public string Name { get; set; }
        public string PluginVersion { get; set; }
        public string MinAppVersion { get; set; }
    }
}