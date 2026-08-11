using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace Cloudless
{
    public partial class LoadingWindow : Window
    {
        public LoadingWindow()
        {
            InitializeComponent();
        }

        private void On_Close(object sender, CancelEventArgs e)
        {
            _vlcCheck?.Stop();
        }

        DispatcherTimer _vlcCheck;

        /// <summary>
        /// Set the loading window message. If provideVlcDetail is true and VLC is not initialized,
        /// a detail message will be shown and it will be hidden automatically once the plugin is ready.
        /// </summary>
        public void SetMessage(string title = "Loading...", string detail = "", bool provideVlcDetail = false)
        {
            try
            {
                if (!string.IsNullOrEmpty(title))
                {
                    TitleTextBlock.Text = title;
                }

                if (!string.IsNullOrEmpty(detail))
                {
                    DetailMessage.Text = detail;
                    DetailMessage.Visibility = Visibility.Visible;
                }
                else
                {
                    DetailMessage.Text = string.Empty;
                    DetailMessage.Visibility = Visibility.Collapsed;
                }

                // If requested, show VLC detail message while plugin is initializing
                if (provideVlcDetail && !PluginInitializationState.IsVlcInitialized)
                {
                    DetailMessage.Text = "Initializing VLC plugin...\nThis can take several seconds.\nIt only needs to be done once, until Cloudless is shutdown.";
                    DetailMessage.Visibility = Visibility.Visible;

                    _vlcCheck = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                    _vlcCheck.Tick += (s, args) =>
                    {
                        if (PluginInitializationState.IsVlcInitialized)
                        {
                            DetailMessage.Visibility = Visibility.Collapsed;
                            DetailMessage.Text = string.Empty;
                            _vlcCheck.Stop();
                        }
                    };
                    _vlcCheck.Start();
                }
            }
            catch
            {
                // best-effort UI update; swallow to avoid breaking caller flows
            }
        }
    }
}
