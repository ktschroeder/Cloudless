using System.Buffers.Binary;
using System.IO;
using System.Text;
using Xunit;

namespace Cloudless.Tests;

public sealed class PngAnimationDetectionTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Cloudless.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void IsPngAnimated_RecognizesAnimationControlChunkAfterOtherChunks()
    {
        var png = CreatePng(
            ("IHDR", new byte[13]),
            ("IDAT", new byte[] { 1, 2, 3 }),
            ("acTL", new byte[8]));

        Assert.True(MainWindow.IsPngAnimated(CreateFile("animated.png", png)));
    }

    [Fact]
    public void IsPngAnimated_DoesNotTreatChunkNameBytesInsideImageDataAsAnimation()
    {
        var png = CreatePng(("IDAT", Encoding.ASCII.GetBytes("acTL")));

        Assert.False(MainWindow.IsPngAnimated(CreateFile("still.png", png)));
    }

    [Theory]
    [InlineData("bad-signature")]
    [InlineData("truncated-header")]
    [InlineData("truncated-payload")]
    [InlineData("truncated-crc")]
    [InlineData("oversized-chunk")]
    [InlineData("invalid-animation-chunk-length")]
    public void IsPngAnimated_RejectsMalformedPngChunkStreams(string malformedKind)
    {
        Assert.False(MainWindow.IsPngAnimated(CreateFile("malformed.png", CreateMalformedPng(malformedKind))));
    }

    [Fact]
    public void IsPngAnimated_ReturnsFalseForMissingFilesAndDirectories()
    {
        Directory.CreateDirectory(_testDirectory);

        Assert.False(MainWindow.IsPngAnimated(Path.Combine(_testDirectory, "missing.png")));
        Assert.False(MainWindow.IsPngAnimated(_testDirectory));
    }

    private string CreateFile(string name, byte[] contents)
    {
        Directory.CreateDirectory(_testDirectory);
        var path = Path.Combine(_testDirectory, name);
        File.WriteAllBytes(path, contents);
        return path;
    }

    private static byte[] CreatePng(params (string Type, byte[] Data)[] chunks)
    {
        using var stream = new MemoryStream();
        stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Span<byte> length = stackalloc byte[4];
        foreach (var (type, data) in chunks)
        {
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
            stream.Write(length);
            stream.Write(Encoding.ASCII.GetBytes(type));
            stream.Write(data);
            stream.Write(new byte[4]);
        }

        return stream.ToArray();
    }

    private static byte[] CreateMalformedPng(string kind)
    {
        var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        using var stream = new MemoryStream();
        stream.Write(signature);

        switch (kind)
        {
            case "bad-signature":
                signature[0] = 0;
                stream.SetLength(0);
                stream.Write(signature);
                stream.Write(CreatePng(("acTL", new byte[8])).AsSpan(8));
                break;
            case "truncated-header":
                stream.Write(new byte[] { 0, 0, 0, 8, (byte)'a', (byte)'c' });
                break;
            case "truncated-payload":
                WriteChunkHeader(stream, "acTL", 8);
                stream.Write(new byte[] { 1, 2 });
                break;
            case "truncated-crc":
                WriteChunkHeader(stream, "acTL", 8);
                stream.Write(new byte[8]);
                break;
            case "oversized-chunk":
                WriteChunkHeader(stream, "IDAT", uint.MaxValue);
                stream.Write(new byte[4]);
                break;
            case "invalid-animation-chunk-length":
                WriteChunkHeader(stream, "acTL", 7);
                stream.Write(new byte[11]);
                break;
        }

        return stream.ToArray();
    }

    private static void WriteChunkHeader(Stream stream, string type, uint length)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, length);
        stream.Write(lengthBytes);
        stream.Write(Encoding.ASCII.GetBytes(type));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }
}
