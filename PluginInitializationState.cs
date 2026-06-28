namespace Cloudless
{
    /// <summary>
    /// Tracks plugin initialization state globally across all window instances.
    /// Plugins should update this to indicate when they are ready for use.
    /// </summary>
    public static class PluginInitializationState
    {
        private static bool _vlcInitialized = false;

        /// <summary>
        /// Gets or sets whether the VLC plugin has been successfully initialized.
        /// This is set to true once a VLC MediaPlayer has been successfully created.
        /// </summary>
        public static bool IsVlcInitialized
        {
            get => _vlcInitialized;
            set => _vlcInitialized = value;
        }
    }
}
