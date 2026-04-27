using Cloudless.PluginBase;
using Cloudless.WebmPlugin;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace Cloudless.WebmPlugin
{
    public class WebmPlayerControl : UserControl, IDisposable, IVideoPlayer
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private VideoView _videoView;
        private IntPtr? _preloadedLibVlcHandle = null;
        private IntPtr? _preloadedLibVlcCoreHandle = null;

        TaskCompletionSource<bool> _loadSignal;

        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr AddDllDirectory(string lpPathName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(uint DirectoryFlags);

        public WebmPlayerControl()
        {
            _loadSignal = new TaskCompletionSource<bool>();

            // Determine plugin assembly folder and expected libvlc RID subfolder
            string? pluginAsmFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(pluginAsmFolder))
                pluginAsmFolder = AppContext.BaseDirectory;

            string pluginLibVlcRoot = Path.Combine(pluginAsmFolder, "libvlc");
            string ridFolder = Environment.Is64BitProcess ? "win-x64" : "win-x86";
            string pluginLibVlcPath = Path.Combine(pluginLibVlcRoot, ridFolder);

            // Host app's libvlc path (where the process will probe)
            string hostBase = AppContext.BaseDirectory ?? Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? "") ?? Environment.CurrentDirectory;
            string hostLibVlcRoot = Path.Combine(hostBase, "libvlc");
            string hostLibVlcPath = Path.Combine(hostLibVlcRoot, ridFolder);

            try
            {
                // If plugin native files exist but host folder doesn't, copy them into host output.
                if (Directory.Exists(pluginLibVlcPath))
                {
                    // Create host folder if missing
                    Directory.CreateDirectory(hostLibVlcPath);

                    // Copy files (overwrite to ensure latest)
                    foreach (var file in Directory.GetFiles(pluginLibVlcPath))
                    {
                        var dest = Path.Combine(hostLibVlcPath, Path.GetFileName(file));
                        try
                        {
                            File.Copy(file, dest, true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to copy native file '{file}' to '{dest}': {ex.Message}");
                        }
                    }

                    // Also copy plugin subfolders (e.g., "plugins" folder) recursively
                    foreach (var dir in Directory.GetDirectories(pluginLibVlcPath))
                    {
                        var destDir = Path.Combine(hostLibVlcPath, Path.GetFileName(dir));
                        CopyDirectoryRecursive(dir, destDir);
                    }
                }
                else
                {
                    Console.WriteLine($"Plugin libvlc path not found: {pluginLibVlcPath}");
                }

                // Now add host libvlc folder to the process DLL search path (modern API)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
                    var addRes = AddDllDirectory(hostLibVlcPath);
                    Console.WriteLine($"AddDllDirectory('{hostLibVlcPath}') returned: {addRes}");

                    // Try preloading for clearer diagnostics
                    string libvlcDll = Path.Combine(hostLibVlcPath, "libvlc.dll");
                    string libvlccoreDll = Path.Combine(hostLibVlcPath, "libvlccore.dll");

                    if (File.Exists(libvlcDll))
                    {
                        try
                        {
                            _preloadedLibVlcHandle = NativeLibrary.Load(libvlcDll);
                            Console.WriteLine($"Preloaded {libvlcDll}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to preload {libvlcDll}: {ex}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"libvlc.dll not found at: {libvlcDll}");
                    }

                    if (File.Exists(libvlccoreDll))
                    {
                        try
                        {
                            _preloadedLibVlcCoreHandle = NativeLibrary.Load(libvlccoreDll);
                            Console.WriteLine($"Preloaded {libvlccoreDll}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to preload {libvlccoreDll}: {ex}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"libvlccore.dll not found at: {libvlccoreDll}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception while preparing native libvlc: {ex}");
            }

            // Finally initialize LibVLCSharp (will throw if native load still fails)
            Core.Initialize();

            // TODO see https://stackoverflow.com/questions/66536923/how-do-i-stop-videoview-control-inside-a-grid-control-from-opening-a-new-window
            //_libVLC = new LibVLC();  // takes like 9 seconds TODO
            //_mediaPlayer = new MediaPlayer(_libVLC);

            //_mediaPlayer.EnableMouseInput = false;
            //_mediaPlayer.EnableKeyInput = false;

            _videoView = new VideoView
            {
                //MediaPlayer = _mediaPlayer,
                //HorizontalAlignment = HorizontalAlignment.Stretch,
                //VerticalAlignment = VerticalAlignment.Stretch
            };
            // we need the VideoView to be fully loaded before setting a MediaPlayer on it.
            _videoView.Loaded += VideoView_Loaded;

            Content = _videoView;
        }

        private void VideoView_Loaded(object sender, RoutedEventArgs e)
        {
            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);

            _mediaPlayer.EnableMouseInput = false;
            _mediaPlayer.EnableKeyInput = false;

            _videoView.MediaPlayer = _mediaPlayer;

            _loadSignal.SetResult(true);
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                try
                {
                    File.Copy(file, destFile, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to copy '{file}' to '{destFile}': {ex.Message}");
                }
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSub = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectoryRecursive(dir, destSub);
            }
        }

        public async Task Play(Uri uri)
        {
            await _loadSignal.Task;  // ensure vide view is loaded, or else VLC will open the media in an external player

            using var media = new Media(_libVLC, uri);
            
            _mediaPlayer?.Play(media);
        }

        public void Pause()
        {
            _mediaPlayer?.Pause();
        }

        public void Stop()
        {
            _mediaPlayer?.Stop();
        }

        public void SetMedia(Uri uri)
        {
            //_mediaPlayer?.Stop();
            //_mediaPlayer?.SetMedia(new Media(_libVLC, uri));
        }

        public void Dispose()
        {
            if (_videoView != null)
            {
                _videoView.MediaPlayer = null;
            }

            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();

            if (_preloadedLibVlcHandle.HasValue)
                NativeLibrary.Free(_preloadedLibVlcHandle.Value);
            if (_preloadedLibVlcCoreHandle.HasValue)
                NativeLibrary.Free(_preloadedLibVlcCoreHandle.Value);
        }
    }
}