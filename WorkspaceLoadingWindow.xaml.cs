using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace Cloudless
{
    public partial class WorkspaceLoadingWindow : Window
    {
        public WorkspaceLoadingWindow()
        {
            InitializeComponent();
        }

        private void On_Close(object sender, CancelEventArgs e)
        {
            _vlcCheck?.Stop();
        }

        DispatcherTimer _vlcCheck;

        public void SetDetailMessage()
        {
            string message = "Initializing VLC plugin...\nThis can take several seconds.\nIt only needs to be done once, until Cloudless is shutdown.";

            if (!PluginInitializationState.IsVlcInitialized)
            {
                DetailMessage.Text = message;
                DetailMessage.Visibility = Visibility.Visible;

                _vlcCheck = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                _vlcCheck.Tick += (s, args) =>
                {
                    if (PluginInitializationState.IsVlcInitialized)
                    {
                        DetailMessage.Visibility = Visibility.Collapsed;
                        DetailMessage.Text = "";
                        _vlcCheck.Stop();
                    }
                };
                _vlcCheck.Start();
            }
        }
    }
}
