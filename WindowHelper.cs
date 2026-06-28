using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

public static class WindowHelper
{
    public static void HandleMouseDown(Window window, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && !IsControlClicked(window, e))
        {
            window.DragMove();
        }
    }

    public static void HandleKeyDown(Window window, KeyEventArgs e)
    {
        // Handle tab navigation with arrow keys and WASD
        if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.A || e.Key == Key.D)
        {
            var tabControl = FindVisualChild<TabControl>(window);
            if (tabControl != null && tabControl.Items.Count > 0)
            {
                int currentIndex = tabControl.SelectedIndex;
                int newIndex = currentIndex;

                // Left or A moves to previous tab
                if (e.Key == Key.Left || e.Key == Key.A)
                {
                    newIndex = (currentIndex - 1 + tabControl.Items.Count) % tabControl.Items.Count;
                }
                // Right or D moves to next tab
                else if (e.Key == Key.Right || e.Key == Key.D)
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

        // Handle window close
        if (Keyboard.Modifiers != ModifierKeys.Control && (e.Key == Key.Escape || e.Key == Key.C))
        {
            window.Close();
            e.Handled = true;
        }
    }

    public static void Close_Click(Window window, RoutedEventArgs e)
    {
        window.Close();
    }

    private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
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

    private static bool IsControlClicked(Window window, MouseButtonEventArgs e)
    {
        var hit = VisualTreeHelper.HitTest(window, e.GetPosition(window));
        return hit?.VisualHit is Button || hit?.VisualHit is ComboBox ||
               hit?.VisualHit is CheckBox || hit?.VisualHit is TextBox;
    }
}
