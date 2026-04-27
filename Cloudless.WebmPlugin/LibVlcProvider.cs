using LibVLCSharp.Shared;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Cloudless.WebmPlugin
{
    /// <summary>
    /// Provides a single shared LibVLC instance for the plugin process and warm-up helpers.
    /// Reusing LibVLC avoids the expensive per-instance initialization.
    /// </summary>
    internal static class LibVlcProvider
    {
        private static readonly object _sync = new();
        private static LibVLC? _instance;
        private static bool _initializedNative = false;

        private static TaskCompletionSource<bool>? _warmUpSignal = null;

        private static LibVLC Instance = null;

        public static async Task<LibVLC> GetInstance(bool isWarmUp = false)
        {
            if (_instance != null) return _instance;

            if (!isWarmUp && _warmUpSignal != null)
            {
                await _warmUpSignal.Task;
                return _instance;
            }

            lock (_sync)
            {
                if (_instance != null) return _instance;
                EnsureNativePlacedAndSearchPath();
                Core.Initialize();
                // Use a focused plugin-path and a few options to reduce probing and UI noise.
                string hostBase = AppContext.BaseDirectory ?? Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? "") ?? Environment.CurrentDirectory;
                string ridFolder = Environment.Is64BitProcess ? "win-x64" : "win-x86";
                string hostLibVlcPath = Path.Combine(hostBase, "libvlc", ridFolder);
                string pluginArg = $"--plugin-path={hostLibVlcPath}";
                _instance = new LibVLC(new[] { pluginArg, "--no-video-title-show", "--no-osd" });
                return _instance;
            }
        }

        /// <summary>
        /// Call early from a background thread to warm up native load and LibVLC initialization.
        /// </summary>
        public static async Task WarmupAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    _warmUpSignal = new TaskCompletionSource<bool>();
                    var _ = GetInstance(isWarmUp: true);
                    _warmUpSignal.SetResult(true);
                }
                catch
                {
                    // swallow; caller can check logs or handle failures
                }
            });
        }

        private static void EnsureNativePlacedAndSearchPath()
        {
            if (_initializedNative) return;
            lock (_sync)
            {
                if (_initializedNative) return;

                // Attempt to locate plugin libvlc folder next to plugin assembly and copy it into host output.
                string? pluginAsmFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(pluginAsmFolder))
                    pluginAsmFolder = AppContext.BaseDirectory;

                string pluginLibVlcRoot = Path.Combine(pluginAsmFolder, "libvlc");
                string ridFolder = Environment.Is64BitProcess ? "win-x64" : "win-x86";
                string pluginLibVlcPath = Path.Combine(pluginLibVlcRoot, ridFolder);

                string hostBase = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
                string hostLibVlcPath = Path.Combine(hostBase, "libvlc", ridFolder);

                try
                {
                    if (Directory.Exists(pluginLibVlcPath))
                    {
                        Directory.CreateDirectory(hostLibVlcPath);

                        foreach (var file in Directory.GetFiles(pluginLibVlcPath))
                        {
                            var dest = Path.Combine(hostLibVlcPath, Path.GetFileName(file));
                            File.Copy(file, dest, true);
                        }

                        foreach (var dir in Directory.GetDirectories(pluginLibVlcPath))
                        {
                            CopyDirectoryRecursive(dir, Path.Combine(hostLibVlcPath, Path.GetFileName(dir)));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"LibVlcProvider: failed copying native files: {ex.Message}");
                }

                // Optionally add hostLibVlcPath via AddDllDirectory/SetDefaultDllDirectories here if needed.
                _initializedNative = true;
            }
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
    }
}