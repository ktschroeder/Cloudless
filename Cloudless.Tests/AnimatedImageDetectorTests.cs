using System.Buffers.Binary;
using System.IO;
using System.Text;
using Xunit;

namespace Cloudless.Tests;

public sealed class AnimatedImageDetectorTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Cloudless.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task IsAnimatedAsync_DetectsMultiFrameGifWithCaseInsensitiveExtension()
    {
        var path = CreateFile(".GIF", CreateGif(frameCount: 2));

        Assert.True(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task IsAnimatedAsync_SkipsEverySupportedGifGlobalColorTableSize(int sizeCode)
    {
        var path = CreateFile(".gif", CreateGif(
            frameCount: 2,
            globalColorTable: true,
            globalColorTableSizeCode: sizeCode));

        Assert.True(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task IsAnimatedAsync_SkipsEverySupportedGifLocalColorTableSize(int sizeCode)
    {
        var path = CreateFile(".gif", CreateGif(
            frameCount: 2,
            localColorTable: true,
            localColorTableSizeCode: sizeCode));

        Assert.True(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Fact]
    public async Task IsAnimatedAsync_CacheInvalidatesWhenFileLengthChanges()
    {
        var path = CreateFile(".gif", CreateGif(frameCount: 1));
        var timestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, timestamp);
        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(path));

        File.WriteAllBytes(path, CreateGif(frameCount: 2));
        File.SetLastWriteTimeUtc(path, timestamp);

        Assert.True(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Fact]
    public async Task IsAnimatedAsync_CacheInvalidatesWhenTimestampChangesWithoutLengthChange()
    {
        var stillGif = CreateGif(frameCount: 1, trailingCommentDataLength: 11);
        var animatedGif = CreateGif(frameCount: 2);
        Assert.Equal(stillGif.Length, animatedGif.Length);
        var path = CreateFile(".gif", stillGif);
        var timestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, timestamp);
        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(path));

        File.WriteAllBytes(path, animatedGif);
        File.SetLastWriteTimeUtc(path, timestamp.AddMinutes(1));

        Assert.True(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Fact]
    public async Task IsAnimatedAsync_ConcurrentCallsReturnConsistentResults()
    {
        var path = CreateFile(".webp", CreateWebp(("ANIM", Array.Empty<byte>())));

        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => AnimatedImageDetector.IsAnimatedAsync(path)));

        Assert.All(results, Assert.True);
    }

    [Fact]
    public async Task IsAnimatedAsync_DetectsFileCreatedAfterAnInitialMissingFileLookup()
    {
        Directory.CreateDirectory(_testDirectory);
        var path = Path.Combine(_testDirectory, "created-later.gif");
        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(path));

        File.WriteAllBytes(path, CreateGif(frameCount: 2));

        Assert.True(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Theory]
    [InlineData("GIF87a")]
    [InlineData("GIF89a")]
    public async Task IsAnimatedAsync_RecognizesBothGifHeaderVersions(string version)
    {
        var path = CreateFile(".gif", CreateGif(frameCount: 2, version: version));

        Assert.True(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Fact]
    public async Task IsAnimatedAsync_SkipsGifColorTablesAndGraphicControlExtensions()
    {
        var path = CreateFile(".gif", CreateGif(
            frameCount: 2,
            globalColorTable: true,
            localColorTable: true,
            graphicControlExtension: true));

        Assert.True(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Fact]
    public async Task IsAnimatedAsync_DoesNotTreatSingleFrameGifAsAnimated()
    {
        var path = CreateFile(".gif", CreateGif(frameCount: 1));

        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Fact]
    public async Task IsAnimatedAsync_DoesNotTreatExtendedWebpWithoutAnimationFlagAsAnimated()
    {
        var path = CreateFile(".webp", CreateWebp(("VP8X", new byte[] { 0x00 })));

        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task IsAnimatedAsync_SkipsWebpChunksWithEvenAndOddPadding(int precedingChunkLength)
    {
        var precedingData = Enumerable.Range(0, precedingChunkLength).Select(value => (byte)value).ToArray();
        var path = CreateFile(".webp", CreateWebp(
            ("JUNK", precedingData),
            ("ANIM", Array.Empty<byte>())));

        Assert.True(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Theory]
    [InlineData("ANIM")]
    [InlineData("ANMF")]
    public async Task IsAnimatedAsync_DetectsAnimatedWebpChunks(string chunkType)
    {
        var path = CreateFile(".webp", CreateWebp((chunkType, Array.Empty<byte>())));

        Assert.True(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Fact]
    public async Task IsAnimatedAsync_DetectsAnimationFlagInExtendedWebpHeader()
    {
        var path = CreateFile(".webp", CreateWebp(("VP8X", new byte[] { 0x02 })));

        Assert.True(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Fact]
    public async Task IsAnimatedAsync_DoesNotTreatStillWebpAsAnimated()
    {
        var path = CreateFile(".webp", CreateWebp(("VP8 ", new byte[] { 0x00 })));

        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Fact]
    public async Task IsAnimatedAsync_ReturnsFalseForUnsupportedExtensionsAndMalformedFiles()
    {
        var unsupportedPath = CreateFile(".png", CreateGif(frameCount: 2));
        var malformedWebp = CreateWebp(("VP8 ", Array.Empty<byte>()));
        BinaryPrimitives.WriteUInt32LittleEndian(malformedWebp.AsSpan(16, 4), uint.MaxValue);
        var malformedPath = CreateFile(".webp", malformedWebp);
        var invalidGifPath = CreateFile(".gif", Encoding.ASCII.GetBytes("GIF89a"));

        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(unsupportedPath));
        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(malformedPath));
        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(invalidGifPath));
        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(Path.Combine(_testDirectory, "missing.gif")));
    }

    [Fact]
    public async Task IsAnimatedAsync_ReturnsFalseForNullBlankAndDirectoryPaths()
    {
        Directory.CreateDirectory(_testDirectory);

        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(null));
        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(" "));
        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(_testDirectory));
    }

    [Theory]
    [InlineData("truncated-global-color-table")]
    [InlineData("truncated-local-color-table")]
    [InlineData("truncated-extension")]
    [InlineData("truncated-image-data")]
    [InlineData("truncated-image-descriptor")]
    [InlineData("truncated-second-image-data")]
    [InlineData("unknown-marker")]
    public async Task IsAnimatedAsync_ReturnsFalseForMalformedGifStructures(string malformedKind)
    {
        var path = CreateFile(".gif", CreateMalformedGif(malformedKind));

        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Fact]
    public async Task IsAnimatedAsync_RejectsInvalidWebpSignaturesAndTruncatedHeaders()
    {
        var badRiff = CreateWebp(("ANIM", Array.Empty<byte>()));
        badRiff[0] = (byte)'X';
        var badWebp = CreateWebp(("ANIM", Array.Empty<byte>()));
        badWebp[8] = (byte)'X';
        var truncated = CreateFile(".webp", Encoding.ASCII.GetBytes("RIFF"));

        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(CreateFile(".webp", badRiff)));
        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(CreateFile(".webp", badWebp)));
        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(truncated));
    }

    [Fact]
    public async Task IsAnimatedAsync_RejectsWebpChunksWhoseDeclaredPayloadExceedsTheContainer()
    {
        var contents = CreateWebp(("JUNK", new byte[] { 1, 2 }));
        BinaryPrimitives.WriteUInt32LittleEndian(contents.AsSpan(16, 4), uint.MaxValue);
        var path = CreateFile(".webp", contents);

        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    [Fact]
    public async Task IsAnimatedAsync_RespectsDeclaredWebpContainerLength()
    {
        var contents = CreateWebp(("ANIM", Array.Empty<byte>()));
        BinaryPrimitives.WriteUInt32LittleEndian(contents.AsSpan(4, 4), 4);
        var path = CreateFile(".webp", contents);

        Assert.False(await AnimatedImageDetector.IsAnimatedAsync(path));
    }

    private string CreateFile(string extension, byte[] contents)
    {
        Directory.CreateDirectory(_testDirectory);
        var path = Path.Combine(_testDirectory, Guid.NewGuid().ToString("N") + extension);
        File.WriteAllBytes(path, contents);
        return path;
    }

    private static byte[] CreateGif(
        int frameCount,
        string version = "GIF89a",
        bool globalColorTable = false,
        bool localColorTable = false,
        bool graphicControlExtension = false,
        int trailingCommentDataLength = 0,
        int globalColorTableSizeCode = 0,
        int localColorTableSizeCode = 0)
    {
        var bytes = new List<byte>(Encoding.ASCII.GetBytes(version));
        bytes.AddRange(new byte[]
        {
            1,
            0,
            1,
            0,
            (byte)(globalColorTable ? 0x80 | globalColorTableSizeCode : 0),
            0,
            0
        });
        if (globalColorTable)
            bytes.AddRange(new byte[3 * (1 << (globalColorTableSizeCode + 1))]);

        for (var i = 0; i < frameCount; i++)
        {
            if (graphicControlExtension)
                bytes.AddRange(new byte[] { 0x21, 0xF9, 4, 0, 0, 0, 0, 0 });

            bytes.Add(0x2C);
            bytes.AddRange(new byte[]
            {
                0,
                0,
                0,
                0,
                1,
                0,
                1,
                0,
                (byte)(localColorTable ? 0x80 | localColorTableSizeCode : 0)
            });
            if (localColorTable)
                bytes.AddRange(new byte[3 * (1 << (localColorTableSizeCode + 1))]);
            bytes.AddRange(new byte[] { 2, 2, 0x44, 0x01, 0 });
        }

        if (trailingCommentDataLength > 0)
        {
            bytes.AddRange(new byte[] { 0x21, 0xFE, (byte)trailingCommentDataLength });
            bytes.AddRange(new byte[trailingCommentDataLength]);
            bytes.Add(0);
        }

        bytes.Add(0x3B);
        return bytes.ToArray();
    }

    private static byte[] CreateMalformedGif(string kind)
    {
        var bytes = new List<byte>(Encoding.ASCII.GetBytes("GIF89a"));
        bytes.AddRange(new byte[] { 1, 0, 1, 0, 0, 0, 0 });

        switch (kind)
        {
            case "truncated-global-color-table":
                bytes[10] = 0x80;
                break;
            case "truncated-local-color-table":
                bytes.Add(0x2C);
                bytes.AddRange(new byte[] { 0, 0, 0, 0, 1, 0, 1, 0, 0x80 });
                break;
            case "truncated-extension":
                bytes.AddRange(new byte[] { 0x21, 0xF9 });
                break;
            case "truncated-image-data":
                bytes.Add(0x2C);
                bytes.AddRange(new byte[] { 0, 0, 0, 0, 1, 0, 1, 0, 0 });
                bytes.AddRange(new byte[] { 2, 4, 0x44 });
                break;
            case "truncated-image-descriptor":
                bytes.AddRange(new byte[] { 0x2C, 0, 0, 0 });
                break;
            case "truncated-second-image-data":
                bytes.Add(0x2C);
                bytes.AddRange(new byte[] { 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2, 0x44, 0x01, 0 });
                bytes.Add(0x2C);
                bytes.AddRange(new byte[] { 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 4, 0x44 });
                break;
            case "unknown-marker":
                bytes.Add(0x00);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return bytes.ToArray();
    }

    private static byte[] CreateWebp(params (string ChunkType, byte[] Data)[] chunks)
    {
        var chunkBytes = new List<byte>();
        foreach (var (chunkType, data) in chunks)
        {
            chunkBytes.AddRange(Encoding.ASCII.GetBytes(chunkType));
            var size = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)data.Length);
            chunkBytes.AddRange(size);
            chunkBytes.AddRange(data);
            if (data.Length % 2 != 0)
                chunkBytes.Add(0);
        }

        var bytes = new List<byte>(Encoding.ASCII.GetBytes("RIFF"));
        var riffSize = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(riffSize, (uint)(4 + chunkBytes.Count));
        bytes.AddRange(riffSize);
        bytes.AddRange(Encoding.ASCII.GetBytes("WEBP"));
        bytes.AddRange(chunkBytes);
        return bytes.ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }
}
