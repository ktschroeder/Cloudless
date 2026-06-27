using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Cloudless
{
    public partial class CommandRefWindow : Window
    {
        public CommandRefWindow()
        {
            InitializeComponent();
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.Right)
            {
                var tabControl = FindVisualChild<TabControl>(this);
                if (tabControl != null && tabControl.Items.Count > 0)
                {
                    int currentIndex = tabControl.SelectedIndex;
                    int newIndex = currentIndex;

                    if (e.Key == Key.Left)
                    {
                        newIndex = (currentIndex - 1 + tabControl.Items.Count) % tabControl.Items.Count;
                    }
                    else if (e.Key == Key.Right)
                    {
                        newIndex = (currentIndex + 1) % tabControl.Items.Count;
                    }

                    if (newIndex != currentIndex)
                    {
                        tabControl.SelectedIndex = newIndex;
                        e.Handled = true;
                        return;
                    }
                }
            }

            WindowHelper.HandleKeyDown(this, e);
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var recursiveResult = FindVisualChild<T>(child);
                if (recursiveResult != null)
                    return recursiveResult;
            }

            return null;
        }
        
        private void Close_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }
    }
}
