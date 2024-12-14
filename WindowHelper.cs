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
        if (e.Key == Key.Escape || e.Key == Key.C)
        {
            window.Close();
            e.Handled = true;
        }
    }

    public static void Close_Click(Window window, RoutedEventArgs e)
    {
        window.Close();
    }

    private static bool IsControlClicked(Window window, MouseButtonEventArgs e)
    {
        var hit = VisualTreeHelper.HitTest(window, e.GetPosition(window));
        return hit?.VisualHit is Button || hit?.VisualHit is ComboBox ||
               hit?.VisualHit is CheckBox || hit?.VisualHit is TextBox;
    }
}
