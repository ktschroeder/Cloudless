using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimpleImageViewer
{



    public partial class MessageHistoryWindow : Window
    {
        private readonly ObservableCollection<string> messageHistory;

        public MessageHistoryWindow(OverlayMessageManager manager)
        {
            InitializeComponent();

            // Get the current session's message history
            messageHistory = manager.GetMessageHistory();

            // Bind the ListBox to the message history
            MessageListBox.ItemsSource = messageHistory;

            // Subscribe to new message notifications
            manager.MessageAdded += OnMessageAdded;
            this.Closed += (s, e) => manager.MessageAdded -= OnMessageAdded;
        }

        private void OnMessageAdded(string message)
        {
            // Ensure UI updates on the UI thread
            Dispatcher.Invoke(() => MessageListBox.ScrollIntoView(message));
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // If the left mouse button is pressed and it's not on a clickable element (e.g., buttons or ComboBox)
            if (e.ChangedButton == MouseButton.Left && !IsControlClicked(e))
            {
                // Allow the window to be dragged
                this.DragMove();
            }
        }

        // Helper method to determine if the click is on a clickable control like a button or dropdown
        private bool IsControlClicked(MouseButtonEventArgs e)
        {
            var hit = VisualTreeHelper.HitTest(this, e.GetPosition(this));

            // Check if the hit test is on a Button or ComboBox (any other controls you want to exclude)
            if (hit?.VisualHit is Button || hit?.VisualHit is ComboBox)
            {
                return true;
            }
            return false;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            //DialogResult = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.C)
            {
                Close();
                e.Handled = true;
                return;
            }
        }
    }
}