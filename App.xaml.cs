using Cloudless;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Cloudless
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            string? filePath = null;

            if (e.Args.Length > 0)
            {
                filePath = e.Args[0];
            }
            Timeline.DesiredFrameRateProperty.OverrideMetadata(
                typeof(Timeline),
                new FrameworkPropertyMetadata { DefaultValue = 60 });

            // Show the main window or a welcome screen
            var mainWindow = new MainWindow(filePath);
            mainWindow.Show();
            
        }
    }
}
