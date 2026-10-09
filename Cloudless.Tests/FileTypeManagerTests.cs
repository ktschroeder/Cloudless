using Xunit;

namespace Cloudless.Tests;

public class FileTypeManagerTests
{
    [Theory]
    [InlineData(".MP4", true)]
    [InlineData("webm", true)]
    [InlineData(".PNG", false)]
    [InlineData("unknown", false)]
    public void IsVideoFile_RecognizesSupportedExtensions(string extension, bool expected)
    {
        Assert.Equal(expected, FileTypeManager.IsVideoFile(extension));
    }

    [Fact]
    public void GetFileTypeByExtension_ReturnsNullForBlankExtension()
    {
        Assert.Null(FileTypeManager.GetFileTypeByExtension("  "));
    }

    [Fact]
    public void FileTypeQueries_ReturnFalseOrNullForNullExtensions()
    {
        Assert.Null(FileTypeManager.GetFileTypeByExtension(null!));
        Assert.False(FileTypeManager.IsVideoFile(null!));
        Assert.False(FileTypeManager.IsPotentiallyAnimatednNonVideo(null!));
    }

    [Theory]
    [InlineData(".MP4", "mp4", true)]
    [InlineData(".wEbP", "webp", false)]
    [InlineData(".unknown", null, false)]
    public void GetFileTypeByExtension_NormalizesInputAndReportsFileType(
        string extension,
        string? expectedExtension,
        bool expectedIsVideo)
    {
        var fileType = FileTypeManager.GetFileTypeByExtension(extension);

        Assert.Equal(expectedExtension, fileType?.Extension);
        Assert.Equal(expectedIsVideo, fileType?.IsVideo ?? false);
    }

    [Theory]
    [InlineData("bmp", false, false)]
    [InlineData("gif", false, true)]
    [InlineData("jfif", false, false)]
    [InlineData("jpeg", false, true)]
    [InlineData("jpg", false, true)]
    [InlineData("mkv", true, true)]
    [InlineData("mp4", true, true)]
    [InlineData("png", false, true)]
    [InlineData("webm", true, true)]
    [InlineData("webp", false, true)]
    public void SupportedExtension_HasConsistentVideoAndAnimationClassification(
        string extension,
        bool expectedVideo,
        bool expectedPotentiallyAnimated)
    {
        Assert.Equal(expectedVideo, FileTypeManager.IsVideoFile("." + extension.ToUpperInvariant()));
        Assert.Equal(expectedPotentiallyAnimated, FileTypeManager.IsPotentiallyAnimatednNonVideo(extension));
    }
}
