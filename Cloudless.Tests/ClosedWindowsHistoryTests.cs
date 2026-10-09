using Xunit;

namespace Cloudless.Tests;

public sealed class ClosedWindowsHistoryTests : IDisposable
{
    public ClosedWindowsHistoryTests()
    {
        ClosedWindowsHistory.ClearHistory();
    }

    [Fact]
    public void RestoreNextClosedWindow_ReturnsMostRecentlyClosedFirst()
    {
        var first = new CloudlessWindowState { ImagePath = "first.jpg" };
        var second = new CloudlessWindowState { ImagePath = "second.jpg" };
        ClosedWindowsHistory.RecordClosedWindow(first);
        ClosedWindowsHistory.RecordClosedWindow(second);

        Assert.Same(second, ClosedWindowsHistory.RestoreNextClosedWindow());
        Assert.Same(first, ClosedWindowsHistory.RestoreNextClosedWindow());
        Assert.Null(ClosedWindowsHistory.RestoreNextClosedWindow());
    }

    [Fact]
    public void RecordClosedWindow_IgnoresNullState()
    {
        ClosedWindowsHistory.RecordClosedWindow(null!);

        Assert.Equal(0, ClosedWindowsHistory.AvailableCount);
        Assert.Null(ClosedWindowsHistory.RestoreNextClosedWindow());
    }

    [Fact]
    public void RecordClosedWindow_KeepsOnlyTheMostRecentOneHundredStates()
    {
        for (var i = 0; i <= 100; i++)
            ClosedWindowsHistory.RecordClosedWindow(new CloudlessWindowState { ImagePath = $"{i}.jpg" });

        Assert.Equal(100, ClosedWindowsHistory.AvailableCount);
        Assert.Equal("100.jpg", ClosedWindowsHistory.RestoreNextClosedWindow()?.ImagePath);
        Assert.Equal("99.jpg", ClosedWindowsHistory.RestoreNextClosedWindow()?.ImagePath);
        Assert.Equal(98, ClosedWindowsHistory.AvailableCount);
    }

    [Fact]
    public void ClearHistory_RemovesAllRecordedStates()
    {
        ClosedWindowsHistory.RecordClosedWindow(new CloudlessWindowState { ImagePath = "image.jpg" });
        ClosedWindowsHistory.ClearHistory();

        Assert.Equal(0, ClosedWindowsHistory.AvailableCount);
        Assert.Null(ClosedWindowsHistory.RestoreNextClosedWindow());
    }

    public void Dispose()
    {
        ClosedWindowsHistory.ClearHistory();
    }
}
