using System.Windows.Threading;
using Xunit;

namespace Cloudless.Tests;

public sealed class SlideshowManagerTests : IDisposable
{
    public SlideshowManagerTests()
    {
        SlideshowManager.Stop();
        SlideshowManager.ClearTriggers();
    }

    [Fact]
    public void Initialize_WithNoActivePagesLeavesSlideshowStopped()
    {
        var startedCount = 0;
        Action onStarted = () => startedCount++;
        SlideshowManager.SlideshowStarted += onStarted;
        try
        {
            SlideshowManager.Initialize(3600, new List<int>(), 0, Dispatcher.CurrentDispatcher, () => { });

            Assert.False(SlideshowManager.IsRunning);
            Assert.Equal(0, startedCount);
        }
        finally
        {
            SlideshowManager.SlideshowStarted -= onStarted;
        }
    }

    [Fact]
    public void Initialize_AdvancesSequentiallyAndWrapsToFirstPage()
    {
        var tickCount = 0;
        Start(new List<int> { 10, 20, 30 }, onTick: () => tickCount++);

        Assert.Null(SlideshowManager.SelectedPage);
        SlideshowManager.NextSlideshowPage();
        Assert.Equal(20, SlideshowManager.SelectedPage);
        SlideshowManager.NextSlideshowPage();
        Assert.Equal(30, SlideshowManager.SelectedPage);
        SlideshowManager.NextSlideshowPage();
        Assert.Equal(10, SlideshowManager.SelectedPage);
        Assert.Equal(3, tickCount);
    }

    [Fact]
    public void Initialize_UsesStartingPageIndexForSequentialAdvance()
    {
        Start(new List<int> { 10, 20, 30 }, startingPageIndex: 1);

        SlideshowManager.NextSlideshowPage();

        Assert.Equal(30, SlideshowManager.SelectedPage);
    }

    [Fact]
    public void Initialize_CopiesActivePageListForTheLifetimeOfTheSlideshow()
    {
        var activePages = new List<int> { 10, 20, 30 };
        Start(activePages);
        activePages.Clear();
        activePages.Add(99);

        SlideshowManager.NextSlideshowPage();

        Assert.Equal(new[] { 10, 20, 30 }, SlideshowManager.CurrentPages);
        Assert.Equal(20, SlideshowManager.SelectedPage);
    }

    [Fact]
    public void NextSlideshowPage_DoesNothingWhenSlideshowIsStopped()
    {
        SlideshowManager.NextSlideshowPage();
        SlideshowManager.SignalTriggerFired(10);

        Assert.Null(SlideshowManager.SelectedPage);
        Assert.False(SlideshowManager.IsRunning);
    }

    [Fact]
    public void SequentialSlideshow_WithOnePageContinuesSelectingThatPage()
    {
        var tickCount = 0;
        Start(new List<int> { 42 }, onTick: () => tickCount++);

        SlideshowManager.NextSlideshowPage();
        SlideshowManager.NextSlideshowPage();

        Assert.Equal(42, SlideshowManager.SelectedPage);
        Assert.Equal(2, tickCount);
    }

    [Fact]
    public void ShuffleWithTwoPages_AlternatesBetweenTheOnlyChoices()
    {
        Start(new List<int> { 1, 2 }, shuffle: true);

        SlideshowManager.NextSlideshowPage();
        Assert.Equal(2, SlideshowManager.SelectedPage);
        SlideshowManager.NextSlideshowPage();

        Assert.Equal(1, SlideshowManager.SelectedPage);
    }

    [Fact]
    public void ShuffleWithOnePage_FallsBackToThatPage()
    {
        var tickCount = 0;
        Start(new List<int> { 7 }, shuffle: true, onTick: () => tickCount++);

        SlideshowManager.NextSlideshowPage();

        Assert.Equal(7, SlideshowManager.SelectedPage);
        Assert.Equal(1, tickCount);
    }

    [Fact]
    public void TriggerOnlySlideshow_SelectsStartingPageAndCanAdvanceManually()
    {
        var tickCount = 0;
        Start(new List<int> { 10, 20, 30 }, startingPageIndex: 1, useTriggers: true, intervalSeconds: 0, onTick: () => tickCount++);

        Assert.True(SlideshowManager.IsRunning);
        Assert.Equal(0, SlideshowManager.CurrentIntervalSeconds);
        Assert.Equal(20, SlideshowManager.SelectedPage);
        SlideshowManager.NextSlideshowPage();

        Assert.Equal(30, SlideshowManager.SelectedPage);
        Assert.Equal(1, tickCount);
    }

    [Fact]
    public void SignalTriggerFired_IgnoresUnregisteredPages()
    {
        SlideshowManager.RegisterTriggerPage(20);
        Start(new List<int> { 10, 20, 30 }, useTriggers: true, intervalSeconds: 0);

        SlideshowManager.SignalTriggerFired(99);

        Assert.Equal(10, SlideshowManager.SelectedPage);
    }

    [Fact]
    public void SignalTriggerFired_IsIgnoredWhenTriggerModeIsDisabled()
    {
        var tickCount = 0;
        SlideshowManager.RegisterTriggerPage(2);
        Start(new List<int> { 1, 2 }, onTick: () => tickCount++);

        SlideshowManager.SignalTriggerFired(2);

        Assert.Null(SlideshowManager.SelectedPage);
        Assert.Equal(0, tickCount);
    }

    [Fact]
    public void TriggerOnlySlideshow_AdvancesWhenRegisteredPageSignals()
    {
        var tickCount = 0;
        SlideshowManager.RegisterTriggerPage(20);
        Start(new List<int> { 10, 20, 30 }, useTriggers: true, intervalSeconds: 0, onTick: () => tickCount++);

        SlideshowManager.SignalTriggerFired(20);

        Assert.Equal(20, SlideshowManager.SelectedPage);
        Assert.Equal(1, tickCount);
    }

    [Fact]
    public void TimedSlideshow_AdvancesWhenNextPageHasNoTrigger()
    {
        var tickCount = 0;
        SlideshowManager.RegisterTriggerPage(99);
        Start(new List<int> { 10, 20 }, useTriggers: true, onTick: () => tickCount++);

        SlideshowManager.NextSlideshowPage();

        Assert.Equal(20, SlideshowManager.SelectedPage);
        Assert.Equal(1, tickCount);
    }

    [Fact]
    public void TimedSlideshow_WaitsWhenNextPageHasTriggerAndResumesOnSignal()
    {
        var tickCount = 0;
        SlideshowManager.RegisterTriggerPage(20);
        Start(new List<int> { 10, 20, 30 }, useTriggers: true, onTick: () => tickCount++);

        SlideshowManager.NextSlideshowPage();
        Assert.Equal(20, SlideshowManager.SelectedPage);
        Assert.Equal(0, tickCount);

        SlideshowManager.SignalTriggerFired(10);
        Assert.Equal(0, tickCount);
        SlideshowManager.SignalTriggerFired(20);

        Assert.Equal(30, SlideshowManager.SelectedPage);
        Assert.Equal(1, tickCount);
    }

    [Fact]
    public void AllPagesHaveTriggers_ReflectsRegisterAndUnregisterOperations()
    {
        Assert.False(SlideshowManager.AllPagesHaveTriggers(new List<int> { 1 }));
        SlideshowManager.RegisterTriggerPage(1);
        SlideshowManager.RegisterTriggerPage(2);
        Assert.True(SlideshowManager.AllPagesHaveTriggers(new List<int> { 1, 2 }));
        Assert.True(SlideshowManager.AllPagesHaveTriggers(new List<int>()));
        Assert.False(SlideshowManager.AllPagesHaveTriggers(new List<int> { 1, 3 }));

        SlideshowManager.UnregisterTriggerPage(1);

        Assert.False(SlideshowManager.AllPagesHaveTriggers(new List<int> { 1, 2 }));
        Assert.False(SlideshowManager.AllPagesHaveTriggers(null!));
    }

    [Fact]
    public void Stop_RaisesStoppedOnceAndResetsSlideshowState()
    {
        var stoppedCount = 0;
        Action onStopped = () => stoppedCount++;
        SlideshowManager.SlideshowStopped += onStopped;
        try
        {
            Start(new List<int> { 1, 2 });
            SlideshowManager.NextSlideshowPage();
            SlideshowManager.Stop();
            SlideshowManager.Stop();

            Assert.Equal(1, stoppedCount);
            Assert.False(SlideshowManager.IsRunning);
            Assert.Null(SlideshowManager.CurrentPages);
            Assert.Equal(0, SlideshowManager.CurrentIntervalSeconds);
            Assert.Null(SlideshowManager.SelectedPage);
            Assert.False(SlideshowManager.UseTriggers);
        }
        finally
        {
            SlideshowManager.SlideshowStopped -= onStopped;
        }
    }

    [Fact]
    public void Stop_PreservesTriggerRegistrationsUntilExplicitlyCleared()
    {
        SlideshowManager.RegisterTriggerPage(5);
        Start(new List<int> { 5, 6 });
        SlideshowManager.Stop();

        Assert.True(SlideshowManager.AllPagesHaveTriggers(new List<int> { 5 }));

        SlideshowManager.ClearTriggers();

        Assert.False(SlideshowManager.AllPagesHaveTriggers(new List<int> { 5 }));
    }

    [Fact]
    public void Initialize_WithEmptyPagesStopsAnExistingSlideshow()
    {
        var stoppedCount = 0;
        Action onStopped = () => stoppedCount++;
        SlideshowManager.SlideshowStopped += onStopped;
        try
        {
            Start(new List<int> { 1, 2 });
            SlideshowManager.Initialize(3600, new List<int>(), 0, Dispatcher.CurrentDispatcher, () => { });

            Assert.False(SlideshowManager.IsRunning);
            Assert.Equal(1, stoppedCount);
        }
        finally
        {
            SlideshowManager.SlideshowStopped -= onStopped;
        }
    }

    [Fact]
    public void ClearTriggers_ResetsTriggerModeAndSelectedPage()
    {
        SlideshowManager.RegisterTriggerPage(2);
        Start(new List<int> { 1, 2 }, useTriggers: true, intervalSeconds: 0);

        SlideshowManager.ClearTriggers();

        Assert.False(SlideshowManager.UseTriggers);
        Assert.Null(SlideshowManager.SelectedPage);
    }

    [Fact]
    public void Shuffle_ChoosesAnotherPageAndAvoidsImmediateReturnWhenThereAreFourPages()
    {
        var pages = new List<int> { 1, 2, 3, 4 };
        Start(pages, shuffle: true);

        SlideshowManager.NextSlideshowPage();
        var firstChoice = SlideshowManager.SelectedPage;
        Assert.NotNull(firstChoice);
        Assert.Contains(firstChoice.Value, pages);
        Assert.NotEqual(1, firstChoice);

        SlideshowManager.NextSlideshowPage();

        Assert.NotNull(SlideshowManager.SelectedPage);
        Assert.Contains(SlideshowManager.SelectedPage.Value, pages);
        Assert.NotEqual(firstChoice, SlideshowManager.SelectedPage);
        Assert.NotEqual(1, SlideshowManager.SelectedPage);
    }

    private static void Start(
        List<int> pages,
        int startingPageIndex = 0,
        bool shuffle = false,
        bool useTriggers = false,
        double intervalSeconds = 3600,
        Action? onTick = null)
    {
        SlideshowManager.Initialize(
            intervalSeconds,
            pages,
            startingPageIndex,
            Dispatcher.CurrentDispatcher,
            onTick ?? (() => { }),
            shuffle,
            useTriggers);
    }

    public void Dispose()
    {
        SlideshowManager.Stop();
        SlideshowManager.ClearTriggers();
    }
}
