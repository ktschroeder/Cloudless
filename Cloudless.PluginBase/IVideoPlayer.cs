using System.Windows.Media;

namespace Cloudless.PluginBase
{
    /// <summary>
    /// Optional typed contract for plugins that provide video playback UI.
    /// Host can cast the returned UIElement to this to control playback.
    /// </summary>
    public interface IVideoPlayer
    {
        Task Play(Uri uri, Task? postPlayTask = null);
        void Pause();
        void Stop();
        /// <summary>
        /// Replace the current media source without starting playback.
        /// </summary>
        void SetMedia(Uri uri);
        Task<(int, int)?> GetDimensions();
        void Dispose();
        void Restart();
        TimeSpan GetDuration();
        /// <summary>
        /// Get the current playback position.
        /// </summary>
        TimeSpan GetPosition();

        /// <summary>
        /// Seek to the specified position (clamped by host if necessary).
        /// </summary>
        void SeekTo(TimeSpan position);
        /// <summary>
        /// Set a loop range for playback. If start or end is null, the media start/end will be used.
        /// </summary>
        void SetLoopRange(TimeSpan? start, TimeSpan? end);
        /// <summary>
        /// Seek forward by a finer granularity, such as by a single frame.
        /// </summary>
        void SeekFineForward();
        /// <summary>
        /// Seek backward by a finer granularity, such as by a single frame.
        /// </summary>
        void SeekFineBackward();
    }

}
