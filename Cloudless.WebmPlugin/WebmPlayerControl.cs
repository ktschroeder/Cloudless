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
        }

        public async Task Initialize()
        {
            _loadSignal = new TaskCompletionSource<bool>();

            _libVLC = await LibVlcProvider.GetInstance();

            

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