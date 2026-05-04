using LibVLCSharp.Shared;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Cloudless.VlcPlugin
{
    /// <summary>
    /// Provides a single shared LibVLC instance for the plugin process and warm-up helpers.
    /// Reusing LibVLC avoids the expensive per-instance initialization.
    /// </summary>
    internal static class LibVlcProvider
    {
        private static readonly object _sync = new();
        private static LibVLC? _instance;
        private static TaskCompletionSource<LibVLC>? _initTcs;

        /// <summary>
        /// Returns the shared LibVLC instance. If initialization is in progress, awaits it.
        /// The first caller triggers background initialization.
        /// </summary>
        public static Task<LibVLC> GetInstance(bool isWarmUp = false)
        {
            lock (_sync)
            {
                if (_instance != null)
                    return Task.FromResult(_instance);

                if (_initTcs != null)
                    return _initTcs.Task;

                _initTcs = new TaskCompletionSource<LibVLC>(TaskCreationOptions.RunContinuationsAsynchronously);

                // Initialize on a background thread so callers don't block the UI thread.
                Task.Run(() =>
                {
                    try
                    {
                        EnsureNativePlacedAndSearchPath();

                        // Initialize LibVLC core once.
                        Core.Initialize();

                        string hostBase = AppContext.BaseDirectory ?? Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? "") ?? Environment.CurrentDirectory;
                        string ridFolder = Environment.Is64BitProcess ? "win-x64" : "win-x86";
                        string hostLibVlcPath = Path.Combine(hostBase, "libvlc", ridFolder);
                        string pluginArg = $"--plugin-path={hostLibVlcPath}";

                        //var lib = new LibVLC(new[] { pluginArg, "--no-video-title-show", "--no-osd" });
                        var lib = new LibVLC(new[] { pluginArg, "--no-video-title-show", "--no-osd", "--no-audio" });

                        lock (_sync)
                        {
                            _instance = lib;
                        }

                        _initTcs?.SetResult(lib);
                    }
                    catch (Exception ex)
                    {
                        _initTcs?.SetException(ex);
                    }
                });

                return _initTcs.Task;
            }
        }

        /// <summary>
        /// Call early from a background thread to warm up native load and LibVLC initialization.
        /// </summary>
        public static async Task WarmupAsync()
        {
            try
            {
                await GetInstance(isWarmUp: true).ConfigureAwait(false);
            }
            catch
            {
                // swallow; callers may not care if warmup fails, actual usage can observe exceptions
            }
        }

        private static bool _initializedNative = false;

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