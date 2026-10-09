using System.Text.Json;
using Xunit;

namespace Cloudless.Tests;

public class WorkspaceSerializationTests
{
    [Fact]
    public void Workspace_RoundTripsWindowAndPageState()
    {
        var workspace = new CloudlessWorkspace
        {
            WorkspaceName = "Review",
            CurrentPageIndex = 3,
            CloudlessWindows =
            {
                new CloudlessWindowState
                {
                    ImagePath = @"C:\Images\sample.mp4",
                    PageIndex = 2,
                    Zoom = 1.75,
                    PanX = -24.5,
                    PanY = 12,
                    LoopStartMs = 1250,
                    LoopEndMs = 5000,
                    IsMuted = true,
                    Volume = 0.4
                }
            }
        };

        var json = JsonSerializer.Serialize(workspace);
        var restored = JsonSerializer.Deserialize<CloudlessWorkspace>(json);

        Assert.NotNull(restored);
        Assert.Equal(workspace.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(workspace.WorkspaceName, restored.WorkspaceName);
        Assert.Equal(workspace.CurrentPageIndex, restored.CurrentPageIndex);
        var window = Assert.Single(restored.CloudlessWindows);
        Assert.Equal(@"C:\Images\sample.mp4", window.ImagePath);
        Assert.Equal(2, window.PageIndex);
        Assert.Equal(1.75, window.Zoom);
        Assert.Equal(-24.5, window.PanX);
        Assert.Equal(12, window.PanY);
        Assert.Equal(1250, window.LoopStartMs);
        Assert.Equal(5000, window.LoopEndMs);
        Assert.True(window.IsMuted);
        Assert.Equal(0.4, window.Volume);
    }

    [Fact]
    public void Workspace_DeserializesAnOlderMinimalFileUsingModelDefaults()
    {
        const string json = "{\"SchemaVersion\":2,\"CurrentPageIndex\":4,\"CloudlessWindows\":[{\"ImagePath\":\"legacy.jpg\"}]}";

        var restored = JsonSerializer.Deserialize<CloudlessWorkspace>(json);

        Assert.NotNull(restored);
        Assert.Equal(2, restored.SchemaVersion);
        Assert.Equal(4, restored.CurrentPageIndex);
        var window = Assert.Single(restored.CloudlessWindows);
        Assert.Equal("legacy.jpg", window.ImagePath);
        Assert.Equal(1, window.PageIndex);
        Assert.Equal(string.Empty, window.DisplayMode);
        Assert.Null(window.LoopStartMs);
        Assert.Null(window.LoopEndMs);
        Assert.Null(window.FlagMs);
        Assert.Null(window.Volume);
        Assert.False(window.IsMaximized);
    }

    [Fact]
    public void Workspace_UsesDefaultsWhenOptionalRootPropertiesAreMissing()
    {
        var restored = JsonSerializer.Deserialize<CloudlessWorkspace>("{}");

        Assert.NotNull(restored);
        Assert.Equal(7, restored.SchemaVersion);
        Assert.Equal(1, restored.CurrentPageIndex);
        Assert.Empty(restored.CloudlessWindows);
        Assert.Null(restored.WorkspaceName);
    }

    [Fact]
    public void Workspace_DeserializationIgnoresUnknownProperties()
    {
        const string json = "{\"SchemaVersion\":7,\"FutureWorkspaceField\":true,\"CloudlessWindows\":[{\"ImagePath\":\"image.png\",\"FutureWindowField\":123}]}";

        var restored = JsonSerializer.Deserialize<CloudlessWorkspace>(json);

        Assert.NotNull(restored);
        Assert.Equal("image.png", Assert.Single(restored.CloudlessWindows).ImagePath);
    }

    [Fact]
    public void Workspace_RoundTripsMultipleWindowsAndExplicitZeroVideoState()
    {
        var workspace = new CloudlessWorkspace
        {
            CloudlessWindows =
            {
                new CloudlessWindowState { ImagePath = "first.jpg", PageIndex = 1, Volume = 0 },
                new CloudlessWindowState { ImagePath = "second.mp4", PageIndex = 4, LoopStartMs = 0, LoopEndMs = 0 }
            }
        };

        var restored = JsonSerializer.Deserialize<CloudlessWorkspace>(JsonSerializer.Serialize(workspace));

        Assert.NotNull(restored);
        Assert.Equal(2, restored.CloudlessWindows.Count);
        Assert.Equal(0, restored.CloudlessWindows[0].Volume);
        Assert.Equal(4, restored.CloudlessWindows[1].PageIndex);
        Assert.Equal(0, restored.CloudlessWindows[1].LoopStartMs);
        Assert.Equal(0, restored.CloudlessWindows[1].LoopEndMs);
    }
}
