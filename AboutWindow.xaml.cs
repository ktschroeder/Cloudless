using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace Cloudless
{
    public partial class AboutWindow : Window
    {
        public AboutWindow(string version)
        {
            InitializeComponent();
            VersionText.Text = "Version: " + version;
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        private void Window_KeyDown(object sender, KeyEventArgs e) { WindowHelper.HandleKeyDown(this, e); }
        private void Close_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }

        private void GitHub_Click(object sender, RoutedEventArgs e)
        {
            const string url = "https://github.com/ktschroeder/Cloudless";

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
}
