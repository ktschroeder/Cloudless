
using System.Windows.Media;

namespace Cloudless.PluginBase
{
    /// <summary>
    /// Optional typed contract for plugins that provide video playback UI.
    /// Host can cast the returned UIElement to this to control playback.
    /// </summary>
    public interface IVideoPlayer
    {
        Task Play(Uri uri);
        void Pause();
        void Stop();
        /// <summary>
        /// Replace the current media source without starting playback.
        /// </summary>
        void SetMedia(Uri uri);
    }

}
