using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

public class OverlayMessageManager
{
    private readonly StackPanel _messageStack;
    private readonly Queue<OverlayMessage> _messageQueue = new();
    private readonly List<TextBlock> _activeMessages = new();

    private ObservableCollection<string> messageHistory = new ObservableCollection<string>();

    public event Action<string>? MessageAdded;

    public OverlayMessageManager(StackPanel messageStack)
    {
        _messageStack = messageStack;
    }

    public void ClearMessageHistory()
    {
        Cloudless.Properties.Settings.Default.SystemMessageHistory = new StringCollection();
        Cloudless.Properties.Settings.Default.Save();
    }
    public List<string> GetMessageHistoryFromSetting()
    {
        List<string> messages = new List<string>();
        var stringCollection = Cloudless.Properties.Settings.Default.SystemMessageHistory;
        if (stringCollection == null)
        {
            messages = new List<string>();
        }
        else
        {
            var list = stringCollection.Cast<string>().ToList();
            messages = list;
        }
        return messages;
    }

    public void WriteToMessageHistory(string message)
    {
        var messages = GetMessageHistoryFromSetting();
        string timestampedMessage = $"{DateTime.Now:HH:mm:ss} - {message}";
        messages.Add(timestampedMessage);

        const int HISTORY_MAX_SIZE = 100;
        StringCollection sc = new StringCollection();
        sc.AddRange(messages.TakeLast(HISTORY_MAX_SIZE).ToArray());
        Cloudless.Properties.Settings.Default.SystemMessageHistory = sc;
        Cloudless.Properties.Settings.Default.Save();
    }

    public void ShowOverlayMessage(string message, TimeSpan duration)
    {
        string timestampedMessage = $"{DateTime.Now:HH:mm:ss} - {message}";
        messageHistory.Add(timestampedMessage);
        MessageAdded?.Invoke(timestampedMessage);

        bool mute = Cloudless.Properties.Settings.Default.MuteMessages;


        if (mute)
        {
            return;
        }

        // Add the message to the queue
        OverlayMessage overlayMessage = new() { Text = message, Duration = duration };
        _messageQueue.Enqueue(overlayMessage);
        WriteToMessageHistory(message);

        // Process the queue if not already active
        if (_activeMessages.Count == 0)
            DisplayNextMessage();
    }

    public ObservableCollection<string> GetMessageHistory() => messageHistory;  // TODO remove redundant bits around here

    private void DisplayNextMessage()
    {
        if (_messageQueue.Count == 0)
            return;

        // Dequeue the next message
        OverlayMessage nextMessage = _messageQueue.Dequeue();

        // Create and configure a TextBlock for the message
        TextBlock messageTextBlock = new()
        {
            Text = nextMessage.Text,
            FontSize = 16,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), // Semi-transparent black
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 5), // Space between messages
            Opacity = 0, // Start hidden
        };

        // Add to the stack and active list
        _messageStack.Children.Add(messageTextBlock);
        _activeMessages.Add(messageTextBlock);

        // Create fade-in animation
        DoubleAnimation fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)));
        messageTextBlock.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        // Schedule fade-out and removal
        Task.Delay(nextMessage.Duration).ContinueWith(_ =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Fade out the message
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(200)));
                fadeOut.Completed += (s, e) =>
                {
                    // Remove the message from the stack
                    _messageStack.Children.Remove(messageTextBlock);
                    _activeMessages.Remove(messageTextBlock);

                    // Slide up remaining messages
                    foreach (var remainingMessage in _activeMessages)
                    {
                        TranslateTransform translateTransform = new();
                        remainingMessage.RenderTransform = translateTransform;
                        DoubleAnimation slideUp = new DoubleAnimation(10, 0, new Duration(TimeSpan.FromMilliseconds(200)));
                        translateTransform.BeginAnimation(TranslateTransform.YProperty, slideUp);
                    }

                    // Display the next message in the queue
                    DisplayNextMessage();
                };
                messageTextBlock.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            });
        });
    }

    private class OverlayMessage
    {
        public string Text { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
    }
}
