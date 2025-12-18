using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace Cloudless
{
    public partial class AboutWindow : Window
    {
        private static Random random = new Random();

        public AboutWindow(string version)
        {
            InitializeComponent();
            VersionText.Text = "Version: " + version;
            MomentOfZen.Text = GetZenText();
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

        private string GetZenText()
        {
            List<string> zenText = new List<string>{
                "You are loved.",
                "You are the universe experiencing itself.",
                "This too shall pass.",
                "Contemplate your thoughts.",
                "Relax each muscle, intentionally and specifically, from head to toe.",
                "Breathe.",
                "Sleep is good for you.",
                "Reach out to them.",
                "Don't take that arms industry job.",
                "If it will take a long time, remember that the time will pass anyway.",
                "How you spend your days is how you spend your life.",
                "What are the odds that you know best?",
                "Take care.",
                "It's okay to change your mind.",
            };

            int index = random.Next(0, zenText.Count);
            return zenText[index];
        }
    }
}
