using Cloudless.PluginBase;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
//using Cloudless.Diagnostics;

namespace Cloudless.VlcPlugin
{
    public class VlcVideoPlayerControl : UserControl, IDisposable, IVideoPlayer
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private VideoView _videoView;
        private IntPtr? _preloadedLibVlcHandle = null;
        private IntPtr? _preloadedLibVlcCoreHandle = null;

        private Uri _currentUri;
        private Media _currentMedia = null;

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
            try { Cloudless.Diagnostics.LeakTracker.Register(this, "VlcVideoPlayerControl"); } catch { }

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
            this.Unloaded += VlcVideoPlayerControl_Unloaded;

            Content = _videoView;
        }

        private void VlcVideoPlayerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Defensive: ensure we try to stop playback if the control is unloaded from visual tree.
            try { Stop(); } catch { }
        }

        private async void VideoView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _libVLC = await LibVlcProvider.GetInstance();
                _mediaPlayer = new MediaPlayer(_libVLC);

                try { Cloudless.Diagnostics.LeakTracker.Register(_mediaPlayer, "LibVLC.MediaPlayer"); } catch { }

                _mediaPlayer.EndReached += (sender, args) =>
                {
                    try
                    {
                        // Note: App seems to crash here sometimes when this event is triggered but the window has been closed. I think in the QueueUserWorkItem method.

                        // IMPORTANT: Restart playback on a different thread to avoid deadlocks
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            //_mediaPlayer.Stop(); // Recommended to stop before re-playing
                            _mediaPlayer.Play(new Media(_libVLC, _currentUri));  // TODO explore hacks for smoth looping... https://stackoverflow.com/questions/56487740/how-to-achieve-looping-playback-with-libvlcsharp  // media.add_option(":input-repeat=65535")
                                                                                 //_videoView.MediaPlayer = _mediaPlayer2;
                                                                                 //_mediaPlayer2.Play();
                        });

                        //Restart();
                    }
                    catch (Exception ex)
                    {
                        // TODO probably pass in messenger to plugins to be used like here
                        Console.WriteLine($"Error in EndReached handler: {ex.Message}");
                    }
                };

                _mediaPlayer.EnableMouseInput = false;
                _mediaPlayer.EnableKeyInput = false;

                // No EndReached handler here to avoid captured closures keeping media player alive.
                _videoView.MediaPlayer = _mediaPlayer;

                _loadSignal.SetResult(true);
            }
            catch (Exception ex)
            {
                // signal load to avoid deadlocks
                _loadSignal.TrySetResult(true);
                Console.WriteLine($"VlcVideoPlayerControl.VideoView_Loaded failed: {ex.Message}");
            }
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
            await _loadSignal.Task;  // ensure video view is loaded, or else VLC will open the media in an external player

            try
            {
                try
                {
                    _mediaPlayer?.Stop();
                }
                catch { }

                try
                {
                    _currentMedia?.Dispose();
                }
                catch { }
                _currentMedia = null;

                // Create and keep the media for the lifetime of playback so we can reliably dispose it later.
                var media = new Media(_libVLC, uri);
                _currentMedia = media;

                try { Cloudless.Diagnostics.LeakTracker.Register(media, "LibVLC.Media"); } catch { }

                _currentUri = uri;

                _mediaPlayer?.Play(media);

                if (postPlayTask != null)
                {
                    await postPlayTask;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VlcVideoPlayerControl.Play failed: {ex.Message}");
                throw;
            }
        }

        public async Task<(int, int)?> GetDimensions()
        {
            await _loadSignal.Task;  // ensure video view is loaded, or else _mediaPlayer is probably null

            if (_mediaPlayer?.Media == null)
                return null;

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
            _mediaPlayer?.SetPause(_mediaPlayer.IsPlaying);
        }

        public void Stop()
        {
            try
            {
                if (_mediaPlayer != null)
                {
                    try { _mediaPlayer.Stop(); } catch { }
                    // Dispose the media used for playback
                    try
                    {
                        _currentMedia?.Dispose();
                    }
                    catch { }
                    _currentMedia = null;
                }
            }
            catch { }
        }

        public void SetMedia(Uri uri)
        {
            // Replace the current media without starting playback.
            //try
            //{
            //    var media = new Media(_libVLC, uri);
            //    try
            //    {
            //        _currentMedia?.Dispose();
            //    }
            //    catch { }
            //    _currentMedia = media;
            //    try { LeakTracker.Register(media, "LibVLC.Media"); } catch { }
            //    _mediaPlayer?.SetMedia(media);
            //}
            //catch { }
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
            try
            {
                if (_videoView != null)
                {
                    _videoView.Loaded -= VideoView_Loaded;
                    this.Unloaded -= VlcVideoPlayerControl_Unloaded;

                    // Detach MediaPlayer from VideoView (MediaPlayer may be same as _mediaPlayer)
                    try
                    {
                        if (_videoView.MediaPlayer != null)
                        {
                            try { _videoView.MediaPlayer.Stop(); } catch { }
                            try { _videoView.MediaPlayer.Dispose(); } catch { }
                            _videoView.MediaPlayer = null;
                        }
                    }
                    catch { }

                    try { _videoView.Dispose(); } catch { }
                }
            }
            catch { }

            try
            {
                // Stop and dispose managed media objects
                //try { _mediaPlayer?.Stop(); } catch { }
                try
                {
                    if (_currentMedia != null)
                    {
                        try { _currentMedia.Dispose(); } catch { }
                        _currentMedia = null;
                    }
                }
                catch { }

                if (_mediaPlayer != null)
                {
                    try { _mediaPlayer.Dispose(); } catch { }
                    _mediaPlayer = null;
                }
            }
            catch { }

            // Do NOT dispose the shared LibVLC instance provided by LibVlcProvider; it is shared across players.
            _libVLC = null;

            try
            {
                if (_preloadedLibVlcHandle.HasValue)
                    NativeLibrary.Free(_preloadedLibVlcHandle.Value);
                if (_preloadedLibVlcCoreHandle.HasValue)
                    NativeLibrary.Free(_preloadedLibVlcCoreHandle.Value);
            }
            catch { }

            try { Cloudless.Diagnostics.LeakTracker.MarkClosed(this); } catch { }
        }

        public TimeSpan GetDuration()
        {
            throw new NotImplementedException();
        }
    }
}