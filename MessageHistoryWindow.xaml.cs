using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Cloudless
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
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        private void Window_KeyDown(object sender, KeyEventArgs e) { WindowHelper.HandleKeyDown(this, e); }
        private void Close_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }

        private void OnMessageAdded(string message)
        {
            // Ensure UI updates on the UI thread
            Dispatcher.Invoke(() => MessageListBox.ScrollIntoView(message));
        }

        private void MessageListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (MessageListBox.SelectedItem is string selectedText)
                {
                    Clipboard.SetText(selectedText);
                }
            }
        }
    }
}