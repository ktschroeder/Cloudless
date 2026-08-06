using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Cloudless
{

    public partial class MessageHistoryWindow : Window
    {
        private readonly List<string> messageHistory;

        public MessageHistoryWindow(OverlayMessageManager manager)
        {
            InitializeComponent();

            // Get the current session's message history
            messageHistory = manager.GetMessageHistoryFromSetting();

            // flip sort order so newer messages are at top
            messageHistory.Reverse();

            // Populate the rich text box with all messages
            UpdateMessageDisplay();

            // Subscribe to new message notifications
            manager.MessageAdded += OnMessageAdded;
            this.Closed += (s, e) => manager.MessageAdded -= OnMessageAdded;
        }

        private void UpdateMessageDisplay()
        {
            // Clear and populate the rich text box
            MessageRichTextBox.Document.Blocks.Clear();

            foreach (var message in messageHistory)
            {
                var paragraph = new Paragraph(new Run(message));
                MessageRichTextBox.Document.Blocks.Add(paragraph);
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        private void Window_KeyDown(object sender, KeyEventArgs e) { WindowHelper.HandleKeyDown(this, e); }
        private void Close_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }

        private void OnMessageAdded(string message)
        {
            // Ensure UI updates on the UI thread
            Dispatcher.Invoke(() =>
            {
                messageHistory.Insert(0, message);
                UpdateMessageDisplay();
            });
        }
    }
}