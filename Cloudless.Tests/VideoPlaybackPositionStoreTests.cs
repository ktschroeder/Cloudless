using System.IO;
using System.Text.Json;
using Xunit;

namespace Cloudless.Tests;

public sealed class VideoPlaybackPositionStoreTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Cloudless.Tests", Guid.NewGuid().ToString("N"));
    private string StorePath => Path.Combine(_testDirectory, "video_positions.json");

    [Fact]
    public void WritePosition_PersistsAndUpdatesExistingMediaPosition()
    {
        var mediaPath = Path.Combine(_testDirectory, "video.mp4");
        using (var store = CreateStore())
        {
            store.WritePosition(mediaPath, TimeSpan.FromSeconds(12));
            store.WritePosition(Path.Combine(_testDirectory, "nested", "..", "video.mp4"), TimeSpan.FromSeconds(18));
        }

        using var reloadedStore = CreateStore();
        Assert.Equal(TimeSpan.FromSeconds(18), reloadedStore.ReadPosition(mediaPath));
        Assert.Single(JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(StorePath))!);
        Assert.False(File.Exists(StorePath + "." + Environment.ProcessId + ".tmp"));
    }

    [Fact]
    public void WritePosition_IgnoresBlankPathsAndNegativePositions()
    {
        using var store = CreateStore();

        store.WritePosition(null, TimeSpan.FromSeconds(1));
        store.WritePosition("  ", TimeSpan.FromSeconds(1));
        store.WritePosition(Path.Combine(_testDirectory, "video.mp4"), TimeSpan.FromTicks(-1));

        Assert.False(File.Exists(StorePath));
        Assert.Null(store.ReadPosition(null));
        Assert.Null(store.ReadPosition("  "));
    }

    [Fact]
    public void ReadAndWritePosition_HandleInvalidPathsWithoutThrowing()
    {
        using var store = CreateStore();

        Assert.Null(store.ReadPosition("\0"));
        store.WritePosition("\0", TimeSpan.FromSeconds(1));

        Assert.False(File.Exists(StorePath));
        Assert.Null(store.ReadPosition(null));
    }

    [Fact]
    public void ReadPosition_IgnoresCorruptAndNegativeStoredTicks()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(StorePath, "not-json");
        using var store = CreateStore();
        var mediaPath = Path.GetFullPath(Path.Combine(_testDirectory, "video.mp4"));

        Assert.Null(store.ReadPosition(mediaPath));

        File.WriteAllText(StorePath, JsonSerializer.Serialize(new Dictionary<string, long>
        {
            [mediaPath] = -1
        }));

        Assert.Null(store.ReadPosition(mediaPath));
    }

    [Fact]
    public void ReadPosition_IgnoresJsonWithTheWrongRootShape()
    {
        Directory.CreateDirectory(_testDirectory);
        using var store = CreateStore();
        var mediaPath = Path.Combine(_testDirectory, "video.mp4");

        foreach (var malformedJson in new[] { "[]", "null", "{\"video.mp4\":\"not ticks\"}" })
        {
            File.WriteAllText(StorePath, malformedJson);
            Assert.Null(store.ReadPosition(mediaPath));
        }
    }

    [Fact]
    public void WritePosition_EvictsOldestEntryWhenStoreExceedsItsLimit()
    {
        Directory.CreateDirectory(_testDirectory);
        var positions = Enumerable.Range(0, 500).ToDictionary(
            index => Path.GetFullPath(Path.Combine(_testDirectory, $"video-{index:D3}.mp4")),
            index => (long)index);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(positions));
        using var store = CreateStore();
        var newPath = Path.Combine(_testDirectory, "new-video.mp4");

        store.WritePosition(newPath, TimeSpan.FromSeconds(1));

        Assert.Null(store.ReadPosition(Path.Combine(_testDirectory, "video-000.mp4")));
        Assert.Equal(TimeSpan.FromTicks(1), store.ReadPosition(Path.Combine(_testDirectory, "video-001.mp4")));
        Assert.Equal(TimeSpan.FromSeconds(1), store.ReadPosition(newPath));
        Assert.Equal(500, JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(StorePath))!.Count);
    }

    [Fact]
    public void WritePosition_MovesAnUpdatedPositionToTheNewestEntryBeforeEviction()
    {
        Directory.CreateDirectory(_testDirectory);
        var positions = Enumerable.Range(0, 500).ToDictionary(
            index => Path.GetFullPath(Path.Combine(_testDirectory, $"video-{index:D3}.mp4")),
            index => (long)index);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(positions));
        using var store = CreateStore();

        store.WritePosition(Path.Combine(_testDirectory, "video-000.mp4"), TimeSpan.FromSeconds(10));
        store.WritePosition(Path.Combine(_testDirectory, "new-video.mp4"), TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(10), store.ReadPosition(Path.Combine(_testDirectory, "video-000.mp4")));
        Assert.Null(store.ReadPosition(Path.Combine(_testDirectory, "video-001.mp4")));
        Assert.Equal(500, JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(StorePath))!.Count);
    }

    [Fact]
    public void ReadPosition_TreatsPathCasingAsEquivalent()
    {
        using var store = CreateStore();
        var mediaPath = Path.Combine(_testDirectory, "Clip.mp4");
        store.WritePosition(mediaPath, TimeSpan.FromSeconds(42));

        Assert.Equal(TimeSpan.FromSeconds(42), store.ReadPosition(mediaPath.ToUpperInvariant()));
    }

    [Fact]
    public void WritePosition_ReplacesCorruptStoreDataWithTheNewValidPosition()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(StorePath, "not-json");
        using var store = CreateStore();
        var mediaPath = Path.Combine(_testDirectory, "recovered.mp4");

        store.WritePosition(mediaPath, TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(5), store.ReadPosition(mediaPath));
        Assert.Single(JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(StorePath))!);
    }

    [Fact]
    public async Task SeparateStoreInstancesWithTheSameMutexPreserveConcurrentWrites()
    {
        var mutexName = $"Local\\CloudlessVideoPlaybackPositionTests_{Guid.NewGuid():N}";
        using var firstStore = new VideoPlaybackPositionStore(StorePath, mutexName);
        using var secondStore = new VideoPlaybackPositionStore(StorePath, mutexName);
        var firstPath = Path.Combine(_testDirectory, "first.mp4");
        var secondPath = Path.Combine(_testDirectory, "second.mp4");

        await Task.WhenAll(
            Task.Run(() => firstStore.WritePosition(firstPath, TimeSpan.FromSeconds(1))),
            Task.Run(() => secondStore.WritePosition(secondPath, TimeSpan.FromSeconds(2))));

        Assert.Equal(TimeSpan.FromSeconds(1), firstStore.ReadPosition(firstPath));
        Assert.Equal(TimeSpan.FromSeconds(2), secondStore.ReadPosition(secondPath));
    }

    private VideoPlaybackPositionStore CreateStore()
    {
        return new VideoPlaybackPositionStore(StorePath, $"Local\\CloudlessVideoPlaybackPositionTests_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }
}
