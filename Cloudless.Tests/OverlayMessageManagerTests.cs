using System.Collections.Specialized;
using System.Windows.Controls;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
public sealed class OverlayMessageManagerTests
{
    [Fact]
    public void WriteToMessageHistory_RetainsOnlyTheMostRecentOneHundredEntries()
    {
        RunOnStaThread(() =>
        {
            var settings = Cloudless.Properties.Settings.Default;
            var originalHistory = settings.SystemMessageHistory?.Cast<string>().ToArray() ?? Array.Empty<string>();
            try
            {
                settings.SystemMessageHistory = new StringCollection();
                var manager = new OverlayMessageManager(new StackPanel());

                for (var i = 0; i < 105; i++)
                    manager.WriteToMessageHistory($"entry-{i}");

                var history = manager.GetMessageHistoryFromSetting();
                Assert.Equal(100, history.Count);
                Assert.Equal("entry-5", GetMessageText(history[0]));
                Assert.Equal("entry-104", GetMessageText(history[^1]));
                Assert.All(history, entry => Assert.Matches(@"^\d{2}:\d{2}:\d{2} - entry-\d+$", entry));
            }
            finally
            {
                RestoreHistory(originalHistory);
            }
        });
    }

    [Fact]
    public void ShowOverlayMessage_WhenMutedStillPublishesAndPersistsWithoutAddingVisuals()
    {
        RunOnStaThread(() =>
        {
            var settings = Cloudless.Properties.Settings.Default;
            var originalHistory = settings.SystemMessageHistory?.Cast<string>().ToArray() ?? Array.Empty<string>();
            try
            {
                settings.SystemMessageHistory = new StringCollection();
                var stack = new StackPanel();
                var manager = new OverlayMessageManager(stack);
                string? publishedMessage = null;
                manager.MessageAdded += message => publishedMessage = message;

                manager.ShowOverlayMessage("muted notification", TimeSpan.FromSeconds(1), mute: true);

                Assert.NotNull(publishedMessage);
                Assert.EndsWith(" - muted notification", publishedMessage);
                Assert.Empty(stack.Children);
                Assert.EndsWith(" - muted notification", Assert.Single(manager.GetMessageHistory()));
                Assert.EndsWith(" - muted notification", Assert.Single(manager.GetMessageHistoryFromSetting()));
            }
            finally
            {
                RestoreHistory(originalHistory);
            }
        });
    }

    private static string GetMessageText(string timestampedMessage) => timestampedMessage.Split(" - ", 2)[1];

    private static void RestoreHistory(IEnumerable<string> originalHistory)
    {
        var restoredHistory = new StringCollection();
        restoredHistory.AddRange(originalHistory.ToArray());
        Cloudless.Properties.Settings.Default.SystemMessageHistory = restoredHistory;
        Cloudless.Properties.Settings.Default.Save();
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
            throw new Xunit.Sdk.XunitException($"Overlay message manager test failed: {failure}");
    }
}
