using Cloudless.PluginBase;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace Cloudless.VlcPlugin
{
    public class VlcVideoPlayerControl : UserControl, IDisposable, IVideoPlayer
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        //private MediaPlayer _mediaPlayer2;
        private VideoView _videoView;
        private IntPtr? _preloadedLibVlcHandle = null;
        private IntPtr? _preloadedLibVlcCoreHandle = null;

        private Uri _currentUri;
        //private Media _media = null; 
        //private Media _media2 = null; 

        TaskCompletionSource<bool> _loadSignal;

        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr AddDllDirectory(string lpPathName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(uint DirectoryFlags);

        public VlcVideoPlayerControl()
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

        private async void VideoView_Loaded(object sender, RoutedEventArgs e)
        {
            _libVLC = await LibVlcProvider.GetInstance();
            _mediaPlayer = new MediaPlayer(_libVLC);

            _mediaPlayer.EnableMouseInput = false;
            _mediaPlayer.EnableKeyInput = false;

            _mediaPlayer.EndReached += (sender, args) =>
            {
                try
                {
                    // IMPORTANT: Restart playback on a different thread to avoid deadlocks
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        //_mediaPlayer.Stop(); // Recommended to stop before re-playing
                        _mediaPlayer.Play(new Media(_libVLC, _currentUri));  // TODO explore hacks for smoth looping... https://stackoverflow.com/questions/56487740/how-to-achieve-looping-playback-with-libvlcsharp  // media.add_option(":input-repeat=65535")
                                                                             //_videoView.MediaPlayer = _mediaPlayer2;
                                                                             //_mediaPlayer2.Play();
                    });
                }
                catch (Exception ex)
                {
                    // TODO probably pass in messenger to plugins to be used like here
                    Console.WriteLine($"Error in EndReached handler: {ex.Message}");
                }
            };

            _videoView.MediaPlayer = _mediaPlayer;

            _loadSignal.SetResult(true);

            // init slot hack for smooth looping, which VLC doesn't support natively
            //_mediaPlayer2 = new MediaPlayer(_libVLC);
            //_mediaPlayer2.EnableMouseInput = false;
            //_mediaPlayer2.EnableKeyInput = false;
            //_mediaPlayer2.Play(_media2);
            //_mediaPlayer2.Pause();
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

        public async Task Play(Uri uri, Task? postPlayTask = null)
        {
            await _loadSignal.Task;  // ensure vide view is loaded, or else VLC will open the media in an external player

            using var media = new Media(_libVLC, uri);
            //_media = media;
            //_media2 = new Media(_libVLC, uri);
            _currentUri = uri;

            _mediaPlayer?.Play(media);

            if (postPlayTask != null)
            {
                await postPlayTask;
            }
        }

        public async Task<(int, int)?> GetDimensions()
        {
            await _loadSignal.Task;  // ensure vide view is loaded, or else _mediaPlayer is probably null

            var tracks = _mediaPlayer.Media.Tracks;
            foreach (var track in tracks)
            {
                if (track.TrackType == TrackType.Video)
                {
                    var videoTrack = track.Data;
                    var width = videoTrack.Video.Width;
                    var height = videoTrack.Video.Height;
                    if (width > 0 && height > 0)
                        return ((int)width, (int)height);
                }
            }
            return null;
        }

        public void Pause()
        {
            //_mediaPlayer?.Pause();
            _mediaPlayer?.SetPause(_mediaPlayer.IsPlaying);
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

        public void Restart()
        {
            if (_mediaPlayer == null || _mediaPlayer.Media == null)
                return;

            if (_mediaPlayer.IsSeekable)
            {
                TimeSpan start = TimeSpan.Zero;
                _mediaPlayer?.SeekTo(start);
            }
            else
            {
                _mediaPlayer?.Stop();
                _mediaPlayer?.Play();
            }
        }

        public void Dispose()
        {
            if (_videoView != null)
            {
                _videoView.MediaPlayer = null;
            }

            _mediaPlayer?.Dispose();
            //_libVLC?.Dispose();

            if (_preloadedLibVlcHandle.HasValue)
                NativeLibrary.Free(_preloadedLibVlcHandle.Value);
            if (_preloadedLibVlcCoreHandle.HasValue)
                NativeLibrary.Free(_preloadedLibVlcCoreHandle.Value);
        }

        public TimeSpan GetDuration()
        {
            throw new NotImplementedException();
        }
    }
}