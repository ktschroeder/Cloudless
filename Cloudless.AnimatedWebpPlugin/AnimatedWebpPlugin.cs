using AnimatedImage.Wpf;
using Cloudless.PluginBase;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Cloudless.VlcPlugin
{
    public class AnimatedWebpPlugin : IPlugin
    {
        public string Name { get => "Animated WEBP Plugin"; }
        public string PluginVersion { get => "1.0.0"; }
        public string MinAppVersion { get => "0.7.4"; }
        public string Description { get => "Animated WEBP Plugin"; }
        public List<string> SupportsFileTypes { get => new List<string> { "webp" }; }

        public ImageSource Convert(byte[] bytes)
        {
            throw new NotImplementedException();
        }

        public async Task<UIElement?> CreateView()
        {
            throw new NotImplementedException();
        }

        public async Task WarmupAsync()
        {
            EnsureNativePlacedAndSearchPath();
            return;
        }

        public void SetAnimatedSource(Image imageDisplay, BitmapImage bitmap)
        {
            ImageBehavior.SetAnimatedSource(imageDisplay, bitmap);
        }

        public object? GetAnimationController(Image imageDisplay)
        {
            var ac = ImageBehavior.GetAnimationController(imageDisplay);
            return ac;
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                try
                {
                    File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CopyDirectoryRecursive failed for '{file}': {ex.Message}");
                }
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectoryRecursive(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
        }

        private static bool _initializedNative = false;
        private static readonly object _sync = new();
        private static void EnsureNativePlacedAndSearchPath()
        {
            if (_initializedNative) return;
            lock (_sync)
            {
                if (_initializedNative) return;

                string? pluginAsmFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(pluginAsmFolder))
                    pluginAsmFolder = AppContext.BaseDirectory;

                string pluginLibRoot = Path.Combine(pluginAsmFolder, "runtimes");
                string ridFolder = Environment.Is64BitProcess ? "win-x64" : "win-x86";
                string pluginLibPath = Path.Combine(pluginLibRoot, ridFolder);

                string hostBase = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
                string hostLibPath = Path.Combine(hostBase, "runtimes", ridFolder);

                try
                {
                    if (Directory.Exists(pluginLibPath))
                    {
                        Directory.CreateDirectory(hostLibPath);

                        foreach (var file in Directory.GetFiles(pluginLibPath))
                        {
                            var dest = Path.Combine(hostLibPath, Path.GetFileName(file));
                            File.Copy(file, dest, true);
                        }

                        foreach (var dir in Directory.GetDirectories(pluginLibPath))
                        {
                            CopyDirectoryRecursive(dir, Path.Combine(hostLibPath, Path.GetFileName(dir)));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"failed copying native files: {ex.Message}");
                }

                _initializedNative = true;
            }
        }
    }
}
