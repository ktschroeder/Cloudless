using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

public class OverlayMessageManager
{
    private readonly StackPanel _messageStack;
    private readonly Queue<OverlayMessage> _messageQueue = new();
    private readonly List<TextBlock> _activeMessages = new();

    private ObservableCollection<string> messageHistory = new ObservableCollection<string>();
    private readonly CancellationTokenSource _cts = new();

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
            Foreground = (Brush)Application.Current.Resources["OverlayForeground"],
            Background = (Brush)Application.Current.Resources["OverlayBackground"],
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 5), // Space between messages
            Opacity = 0, // Start hidden
        };

        // Bind to dynamic resources so messages update when theme changes
        messageTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "OverlayForeground");
        messageTextBlock.SetResourceReference(TextBlock.BackgroundProperty, "OverlayBackground");

        // Add to the stack and active list
        _messageStack.Children.Add(messageTextBlock);
        _activeMessages.Add(messageTextBlock);

        // Create fade-in animation
        DoubleAnimation fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)));
        messageTextBlock.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        // Schedule fade-out and removal using a cancellable token so pending continuations don't keep window alive
        var token = _cts.Token;
        Task.Delay(nextMessage.Duration, token).ContinueWith(t =>
        {
            if (t.IsCanceled)
            {
                // If cancelled, ensure the message is removed if present
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_messageStack.Children.Contains(messageTextBlock))
                        _messageStack.Children.Remove(messageTextBlock);
                    _activeMessages.Remove(messageTextBlock);
                });
                return;
            }

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
        }, token, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
        }
        catch { }

        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Remove any active message visuals
                foreach (var tb in _activeMessages.ToList())
                {
                    if (_messageStack.Children.Contains(tb))
                        _messageStack.Children.Remove(tb);
                }
                _activeMessages.Clear();
            });
        }
        catch { }

        _messageQueue.Clear();
        MessageAdded = null;

        try { _cts.Dispose(); } catch { }
    }

    private class OverlayMessage
    {
        public string Text { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
    }
}
