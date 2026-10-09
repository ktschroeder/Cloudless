using System.IO;
using System.Text.Json;
using Xunit;

namespace Cloudless.Tests;

public sealed class WorkspacePersistenceTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Cloudless.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTripWindowPlacementViewStateAndVideoFeatures()
    {
        var expected = new CloudlessWorkspace
        {
            WorkspaceName = "Production review",
            SchemaVersion = 7,
            CurrentPageIndex = 4,
            CloudlessWindows =
            {
                new CloudlessWindowState
                {
                    ImagePath = @"C:\Media\clip.mp4",
                    Left = -1920.5,
                    Top = 120.25,
                    Width = 1280,
                    Height = 720,
                    DisplayMode = "BestFit",
                    Zoom = 1.75,
                    PanX = -44.5,
                    PanY = 32.25,
                    RenderWidth = 1920,
                    RenderHeight = 1080,
                    LoopStartMs = 1500,
                    LoopEndMs = 8750,
                    FlagMs = 4200,
                    SlideshowTriggerCount = 3,
                    IsSynced = true,
                    IsMuted = false,
                    Volume = 0.65,
                    IsMaximized = true,
                    IsMinimized = false,
                    ZOrder = 2,
                    MonitorLeft = -1920,
                    MonitorTop = 0,
                    MonitorWidth = 1920,
                    MonitorHeight = 1080,
                    CloudlessAppVersion = "1.2.3",
                    PageIndex = 4,
                    WindowWasMinimizedPriorToHidingForPage = true,
                    WindowWasMaximizedPriorToHidingForPage = false
                },
                new CloudlessWindowState
                {
                    ImagePath = @"D:\Pictures\still.jpg",
                    Left = 20,
                    Top = 30,
                    Width = 800,
                    Height = 600,
                    Zoom = 2,
                    PanX = 15,
                    PanY = -10,
                    RenderWidth = 400,
                    RenderHeight = 300,
                    ZOrder = 1,
                    PageIndex = 1
                }
            }
        };
        var path = Path.Combine(_testDirectory, "nested", "review.cloudless");

        WorkspacePersistence.Save(path, expected);
        var restored = WorkspacePersistence.Load(path);

        Assert.NotNull(restored);
        Assert.Equal(expected.WorkspaceName, restored.WorkspaceName);
        Assert.Equal(expected.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(expected.CurrentPageIndex, restored.CurrentPageIndex);
        Assert.Equal(2, restored.CloudlessWindows.Count);
        AssertWindowStateEqual(expected.CloudlessWindows[0], restored.CloudlessWindows[0]);
        AssertWindowStateEqual(expected.CloudlessWindows[1], restored.CloudlessWindows[1]);
    }

    [Fact]
    public void Save_CreatesParentDirectoryAndWritesIndentedWorkspaceJson()
    {
        var path = Path.Combine(_testDirectory, "new-folder", "workspace.cloudless");

        WorkspacePersistence.Save(path, new CloudlessWorkspace());

        Assert.True(File.Exists(path));
        Assert.Contains(Environment.NewLine, File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public void Save_OverwritesExistingWorkspaceWithoutRetainingOldWindows()
    {
        var path = Path.Combine(_testDirectory, "workspace.cloudless");
        WorkspacePersistence.Save(path, new CloudlessWorkspace
        {
            CloudlessWindows = { new CloudlessWindowState { ImagePath = "old.jpg" } }
        });
        WorkspacePersistence.Save(path, new CloudlessWorkspace
        {
            CloudlessWindows = { new CloudlessWindowState { ImagePath = "new.jpg" } }
        });

        var restored = WorkspacePersistence.Load(path);

        Assert.NotNull(restored);
        Assert.Equal("new.jpg", Assert.Single(restored.CloudlessWindows).ImagePath);
    }

    [Fact]
    public void Load_ReturnsNullWhenTheWorkspaceDocumentContainsJsonNull()
    {
        var path = Path.Combine(_testDirectory, "null.cloudless");
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(path, "null");

        Assert.Null(WorkspacePersistence.Load(path));
    }

    [Fact]
    public void Load_NormalizesExplicitlyNullWindowCollectionToAnEmptyList()
    {
        Directory.CreateDirectory(_testDirectory);
        var path = Path.Combine(_testDirectory, "null-windows.cloudless");
        File.WriteAllText(path, "{\"CloudlessWindows\":null}");

        var restored = WorkspacePersistence.Load(path);

        Assert.NotNull(restored);
        Assert.Empty(restored.CloudlessWindows);
    }

    [Fact]
    public void Load_RejectsNullWindowEntriesRatherThanReturningAnUnusableWorkspace()
    {
        Directory.CreateDirectory(_testDirectory);
        var path = Path.Combine(_testDirectory, "null-window.cloudless");
        File.WriteAllText(path, "{\"CloudlessWindows\":[null]}");

        Assert.Throws<JsonException>(() => WorkspacePersistence.Load(path));
    }

    [Fact]
    public void Load_PropagatesMalformedJsonInsteadOfReturningPartialWorkspace()
    {
        var path = Path.Combine(_testDirectory, "broken.cloudless");
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(path, "{\"CloudlessWindows\":[");

        Assert.Throws<JsonException>(() => WorkspacePersistence.Load(path));
    }

    [Fact]
    public void Load_ReportsMissingWorkspaceFile()
    {
        Directory.CreateDirectory(_testDirectory);
        var path = Path.Combine(_testDirectory, "missing.cloudless");

        Assert.Throws<FileNotFoundException>(() => WorkspacePersistence.Load(path));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"not a workspace\"")]
    public void Load_RejectsValidJsonWithAnInvalidRootType(string json)
    {
        Directory.CreateDirectory(_testDirectory);
        var path = Path.Combine(_testDirectory, "wrong-root.cloudless");
        File.WriteAllText(path, json);

        Assert.Throws<JsonException>(() => WorkspacePersistence.Load(path));
    }

    [Fact]
    public void Save_RejectsNullWorkspaceBeforeCreatingAnyFiles()
    {
        var path = Path.Combine(_testDirectory, "null-workspace.cloudless");

        Assert.Throws<ArgumentNullException>(() => WorkspacePersistence.Save(path, null!));
        Assert.False(Directory.Exists(_testDirectory));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void SaveAndLoad_RejectBlankFilePaths(string path)
    {
        Assert.Throws<ArgumentException>(() => WorkspacePersistence.Save(path, new CloudlessWorkspace()));
        Assert.Throws<ArgumentException>(() => WorkspacePersistence.Load(path));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, false)]
    [InlineData(100, true)]
    public void SaveAndLoad_PreservesSlideshowTriggerAndSyncCombinations(int? triggerCount, bool? isSynced)
    {
        var expectedWindow = new CloudlessWindowState
        {
            ImagePath = @"C:\Media\triggered.webm",
            PageIndex = 3,
            SlideshowTriggerCount = triggerCount,
            IsSynced = isSynced
        };

        var restored = RoundTrip(new CloudlessWorkspace
        {
            CurrentPageIndex = 3,
            CloudlessWindows = { expectedWindow }
        });

        var actualWindow = Assert.Single(restored.CloudlessWindows);
        Assert.Equal(triggerCount, actualWindow.SlideshowTriggerCount);
        Assert.Equal(isSynced, actualWindow.IsSynced);
        Assert.Equal(3, actualWindow.PageIndex);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0d, null)]
    [InlineData(null, 0d)]
    [InlineData(0d, 0d)]
    [InlineData(1500d, 6000d)]
    [InlineData(6000d, 1500d)]
    public void SaveAndLoad_PreservesPartialZeroAndReversedVideoLoopBounds(double? loopStartMs, double? loopEndMs)
    {
        var expectedWindow = new CloudlessWindowState
        {
            ImagePath = @"C:\Media\loop.mkv",
            LoopStartMs = loopStartMs,
            LoopEndMs = loopEndMs,
            FlagMs = 4200
        };

        var restoredWindow = Assert.Single(RoundTrip(new CloudlessWorkspace
        {
            CloudlessWindows = { expectedWindow }
        }).CloudlessWindows);

        Assert.Equal(loopStartMs, restoredWindow.LoopStartMs);
        Assert.Equal(loopEndMs, restoredWindow.LoopEndMs);
        Assert.Equal(4200, restoredWindow.FlagMs);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(true, 0d)]
    [InlineData(false, 1d)]
    [InlineData(true, 0.35)]
    [InlineData(null, 0d)]
    [InlineData(false, null)]
    public void SaveAndLoad_PreservesOptionalMuteAndVolumeCombinations(bool? isMuted, double? volume)
    {
        var restoredWindow = Assert.Single(RoundTrip(new CloudlessWorkspace
        {
            CloudlessWindows =
            {
                new CloudlessWindowState
                {
                    ImagePath = "audio-state.mp4",
                    IsMuted = isMuted,
                    Volume = volume
                }
            }
        }).CloudlessWindows);

        Assert.Equal(isMuted, restoredWindow.IsMuted);
        Assert.Equal(volume, restoredWindow.Volume);
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void SaveAndLoad_PreservesWindowAndPageVisibilityFlags(
        bool isMaximized,
        bool isMinimized,
        bool wasMaximizedBeforePageHide,
        bool wasMinimizedBeforePageHide)
    {
        var restoredWindow = Assert.Single(RoundTrip(new CloudlessWorkspace
        {
            CloudlessWindows =
            {
                new CloudlessWindowState
                {
                    IsMaximized = isMaximized,
                    IsMinimized = isMinimized,
                    WindowWasMaximizedPriorToHidingForPage = wasMaximizedBeforePageHide,
                    WindowWasMinimizedPriorToHidingForPage = wasMinimizedBeforePageHide
                }
            }
        }).CloudlessWindows);

        Assert.Equal(isMaximized, restoredWindow.IsMaximized);
        Assert.Equal(isMinimized, restoredWindow.IsMinimized);
        Assert.Equal(wasMaximizedBeforePageHide, restoredWindow.WindowWasMaximizedPriorToHidingForPage);
        Assert.Equal(wasMinimizedBeforePageHide, restoredWindow.WindowWasMinimizedPriorToHidingForPage);
    }

    [Fact]
    public void SaveAndLoad_PreservesInterleavedPageAndZOrderAssignmentsInWindowOrder()
    {
        var workspace = new CloudlessWorkspace
        {
            CurrentPageIndex = 7,
            CloudlessWindows =
            {
                new CloudlessWindowState { ImagePath = "first.jpg", PageIndex = 7, ZOrder = 3 },
                new CloudlessWindowState { ImagePath = "second.mp4", PageIndex = 2, ZOrder = 0 },
                new CloudlessWindowState { ImagePath = "third.jpg", PageIndex = 7, ZOrder = 1 },
                new CloudlessWindowState { ImagePath = "fourth.webp", PageIndex = 20, ZOrder = 2 }
            }
        };

        var restored = RoundTrip(workspace);

        Assert.Equal(7, restored.CurrentPageIndex);
        Assert.Collection(
            restored.CloudlessWindows,
            window => { Assert.Equal("first.jpg", window.ImagePath); Assert.Equal(7, window.PageIndex); Assert.Equal(3, window.ZOrder); },
            window => { Assert.Equal("second.mp4", window.ImagePath); Assert.Equal(2, window.PageIndex); Assert.Equal(0, window.ZOrder); },
            window => { Assert.Equal("third.jpg", window.ImagePath); Assert.Equal(7, window.PageIndex); Assert.Equal(1, window.ZOrder); },
            window => { Assert.Equal("fourth.webp", window.ImagePath); Assert.Equal(20, window.PageIndex); Assert.Equal(2, window.ZOrder); });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    public void SaveAndLoad_PreservesSchemaVersionWithoutImplicitMigration(int schemaVersion)
    {
        var restored = RoundTrip(new CloudlessWorkspace { SchemaVersion = schemaVersion });

        Assert.Equal(schemaVersion, restored.SchemaVersion);
    }

    [Fact]
    public void SaveAndLoad_PreservesEmptyWorkspaceAndEmptyMediaPathWindow()
    {
        var emptyWorkspace = RoundTrip(new CloudlessWorkspace());
        var emptyWindowWorkspace = RoundTrip(new CloudlessWorkspace
        {
            CloudlessWindows = { new CloudlessWindowState { ImagePath = string.Empty } }
        });

        Assert.Empty(emptyWorkspace.CloudlessWindows);
        Assert.Equal(string.Empty, Assert.Single(emptyWindowWorkspace.CloudlessWindows).ImagePath);
    }

    [Theory]
    [InlineData("origin", true)]
    [InlineData(" ORIGIN ", true)]
    [InlineData("undoload", true)]
    [InlineData("QuickSave", true)]
    [InlineData("_system_internal", true)]
    [InlineData("MainWorkspace", false)]
    [InlineData("system_backup", false)]
    public void IsReservedWorkspaceName_HandlesReservedNamesCaseInsensitively(string name, bool expected)
    {
        Assert.Equal(expected, MainWindow.IsReservedWorkspaceName(name));
    }

    private CloudlessWorkspace RoundTrip(CloudlessWorkspace workspace)
    {
        var path = Path.Combine(_testDirectory, Guid.NewGuid().ToString("N"), "workspace.cloudless");
        WorkspacePersistence.Save(path, workspace);
        return WorkspacePersistence.Load(path) ?? throw new InvalidDataException("Saved workspace deserialized to null.");
    }

    private static void AssertWindowStateEqual(CloudlessWindowState expected, CloudlessWindowState actual)
    {
        Assert.Equal(expected.ImagePath, actual.ImagePath);
        Assert.Equal(expected.Left, actual.Left);
        Assert.Equal(expected.Top, actual.Top);
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.DisplayMode, actual.DisplayMode);
        Assert.Equal(expected.Zoom, actual.Zoom);
        Assert.Equal(expected.PanX, actual.PanX);
        Assert.Equal(expected.PanY, actual.PanY);
        Assert.Equal(expected.RenderWidth, actual.RenderWidth);
        Assert.Equal(expected.RenderHeight, actual.RenderHeight);
        Assert.Equal(expected.LoopStartMs, actual.LoopStartMs);
        Assert.Equal(expected.LoopEndMs, actual.LoopEndMs);
        Assert.Equal(expected.FlagMs, actual.FlagMs);
        Assert.Equal(expected.SlideshowTriggerCount, actual.SlideshowTriggerCount);
        Assert.Equal(expected.IsSynced, actual.IsSynced);
        Assert.Equal(expected.IsMuted, actual.IsMuted);
        Assert.Equal(expected.Volume, actual.Volume);
        Assert.Equal(expected.IsMaximized, actual.IsMaximized);
        Assert.Equal(expected.IsMinimized, actual.IsMinimized);
        Assert.Equal(expected.ZOrder, actual.ZOrder);
        Assert.Equal(expected.MonitorLeft, actual.MonitorLeft);
        Assert.Equal(expected.MonitorTop, actual.MonitorTop);
        Assert.Equal(expected.MonitorWidth, actual.MonitorWidth);
        Assert.Equal(expected.MonitorHeight, actual.MonitorHeight);
        Assert.Equal(expected.CloudlessAppVersion, actual.CloudlessAppVersion);
        Assert.Equal(expected.PageIndex, actual.PageIndex);
        Assert.Equal(expected.WindowWasMinimizedPriorToHidingForPage, actual.WindowWasMinimizedPriorToHidingForPage);
        Assert.Equal(expected.WindowWasMaximizedPriorToHidingForPage, actual.WindowWasMaximizedPriorToHidingForPage);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }
}
