using System.IO;
using Xunit;

namespace Cloudless.Tests;

public sealed class TagManagerPersistenceTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Cloudless.Tests", Guid.NewGuid().ToString("N"));
    private string TagsFilePath => Path.Combine(_testDirectory, "tags.json");

    [Fact]
    public void Tags_PersistAndBooleanQueriesReturnMatchingFiles()
    {
        Directory.CreateDirectory(_testDirectory);
        var firstFile = Path.Combine(_testDirectory, "first.jpg");
        var secondFile = Path.Combine(_testDirectory, "second.jpg");
        var thirdFile = Path.Combine(_testDirectory, "third.jpg");
        var manager = new TagManager(TagsFilePath);

        manager.AddTags(firstFile, "travel", "edited");
        manager.AddTags(secondFile, "family");
        manager.AddTags(thirdFile, "travel");

        var reloadedManager = new TagManager(TagsFilePath);
        reloadedManager.Load();

        Assert.Equal(new[] { "edited", "travel" }, reloadedManager.GetTagsForFile(firstFile));
        Assert.Equal(
            new[] { Path.GetFullPath(secondFile).ToLowerInvariant(), Path.GetFullPath(thirdFile).ToLowerInvariant() }.OrderBy(path => path),
            reloadedManager.QueryTags("(travel OR family) AND NOT edited"));
    }

    [Fact]
    public void QueryTags_AppliesAndBeforeOrAndSupportsParentheses()
    {
        Directory.CreateDirectory(_testDirectory);
        var landscape = Path.Combine(_testDirectory, "landscape.jpg");
        var edited = Path.Combine(_testDirectory, "edited.jpg");
        var family = Path.Combine(_testDirectory, "family.jpg");
        var manager = new TagManager(TagsFilePath);

        manager.AddTags(landscape, "landscape", "edited");
        manager.AddTags(edited, "landscape");
        manager.AddTags(family, "family");

        Assert.Equal(
            new[] { Path.GetFullPath(edited).ToLowerInvariant(), Path.GetFullPath(landscape).ToLowerInvariant() },
            manager.QueryTags("LANDSCAPE OR family AND edited"));
        Assert.Equal(
            new[] { Path.GetFullPath(edited).ToLowerInvariant(), Path.GetFullPath(family).ToLowerInvariant() }.OrderBy(path => path),
            manager.QueryTags("(landscape OR family) AND NOT edited"));
    }

    [Theory]
    [InlineData("travel family")]
    [InlineData("travel)")]
    [InlineData("(travel")]
    [InlineData("travel OR")]
    [InlineData("AND travel")]
    [InlineData("()")]
    [InlineData("travel AND AND family")]
    [InlineData("travel OR (family")]
    [InlineData("travel AND )")]
    [InlineData("(travel))")]
    [InlineData("travel OR OR family")]
    [InlineData("NOT NOT")]
    public void QueryTags_ReturnsNoPartialMatchesForMalformedExpressions(string query)
    {
        Directory.CreateDirectory(_testDirectory);
        var manager = new TagManager(TagsFilePath);
        manager.AddTags(Path.Combine(_testDirectory, "travel.jpg"), "travel");

        Assert.Empty(manager.QueryTags(query));
    }

    [Theory]
    [InlineData("travel", "a.jpg,c.jpg")]
    [InlineData("travel AND family", "c.jpg")]
    [InlineData("travel OR family", "a.jpg,b.jpg,c.jpg,d.jpg")]
    [InlineData("travel OR family AND edited", "a.jpg,c.jpg,d.jpg")]
    [InlineData("(travel OR family) AND NOT edited", "b.jpg,c.jpg")]
    [InlineData("NOT travel", "b.jpg,d.jpg")]
    [InlineData("NOT (travel AND family)", "a.jpg,b.jpg,d.jpg")]
    [InlineData("family AND (travel OR edited)", "c.jpg,d.jpg")]
    [InlineData("NOT missing", "a.jpg,b.jpg,c.jpg,d.jpg")]
    [InlineData("NOT NOT travel", "a.jpg,c.jpg")]
    [InlineData("NOT NOT NOT travel", "b.jpg,d.jpg")]
    [InlineData("NOT NOT (family AND edited)", "d.jpg")]
    [InlineData("(travel OR family) AND (NOT edited)", "b.jpg,c.jpg")]
    [InlineData("travel\nAND\tfamily", "c.jpg")]
    [InlineData("TRAVEL Or FAMILY", "a.jpg,b.jpg,c.jpg,d.jpg")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    public void QueryTags_EvaluatesBooleanExpressionsWithExpectedPrecedence(string query, string expectedFileNames)
    {
        Directory.CreateDirectory(_testDirectory);
        var manager = new TagManager(TagsFilePath);
        manager.AddTags(Path.Combine(_testDirectory, "a.jpg"), "travel", "edited");
        manager.AddTags(Path.Combine(_testDirectory, "b.jpg"), "family");
        manager.AddTags(Path.Combine(_testDirectory, "c.jpg"), "travel", "family");
        manager.AddTags(Path.Combine(_testDirectory, "d.jpg"), "family", "edited");

        var expected = expectedFileNames.Split(',', StringSplitOptions.RemoveEmptyEntries).OrderBy(name => name);
        var actual = manager.QueryTags(query).Select(path => Path.GetFileName(path)!).OrderBy(name => name);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Tags_NormalizeCaseAndWhitespaceAndRejectReservedKeywords()
    {
        Directory.CreateDirectory(_testDirectory);
        var file = Path.Combine(_testDirectory, "image.jpg");
        var manager = new TagManager(TagsFilePath);

        manager.AddTags(file, " Travel ", "travel", "AND");

        Assert.Equal(new[] { "travel" }, manager.GetTagsForFile(file));
        Assert.Equal(new[] { Path.GetFullPath(file).ToLowerInvariant() }, manager.QueryTags("TRAVEL"));
    }

    [Fact]
    public void Load_MalformedJsonStartsEmptyAndCanBeReplacedByAValidMutation()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(TagsFilePath, "{\"Tags\":[");
        var manager = new TagManager(TagsFilePath);

        manager.Load();

        Assert.Empty(manager.GetAllTags());
        manager.AddTags(Path.Combine(_testDirectory, "recovered.jpg"), "recovered");

        var reloadedManager = new TagManager(TagsFilePath);
        reloadedManager.Load();
        Assert.Equal(new[] { "recovered" }, reloadedManager.GetAllTags());
    }

    [Fact]
    public void AddTags_ReportsBlankReservedDuplicateAndAcceptedTagsIndividually()
    {
        Directory.CreateDirectory(_testDirectory);
        var file = Path.Combine(_testDirectory, "image.jpg");
        var manager = new TagManager(TagsFilePath);

        var messages = manager.AddTags(file, " ", "AND", "travel", "TRAVEL");

        Assert.Contains("Tag cannot be empty.", messages);
        Assert.Contains("Cannot use reserved keyword 'AND' as a tag.", messages);
        Assert.Contains("Added tag 'travel' to file.", messages);
        Assert.Contains("File already has tag 'TRAVEL'.", messages);
        Assert.Equal(new[] { "travel" }, manager.GetTagsForFile(file));
    }

    [Fact]
    public void QueryTags_ReturnsNoMatchesForNullOrEmptyQueries()
    {
        var manager = new TagManager(TagsFilePath);
        manager.AddTags(Path.Combine(_testDirectory, "image.jpg"), "travel");

        Assert.Empty(manager.QueryTags(null!));
        Assert.Empty(manager.QueryTags(string.Empty));
        Assert.Empty(manager.QueryTags("   "));
    }

    [Fact]
    public void DestroyTag_ReportsMissingTagAndLeavesExistingTagsUntouched()
    {
        var manager = new TagManager(TagsFilePath);
        var file = Path.Combine(_testDirectory, "image.jpg");
        manager.AddTags(file, "travel");

        Assert.Equal("Tag 'missing' does not exist.", manager.DestroyTag("missing"));
        Assert.Equal(new[] { "travel" }, manager.GetTagsForFile(file));
    }

    [Fact]
    public void RemoveTagsAndDestroyTag_UpdatePersistedIndexes()
    {
        Directory.CreateDirectory(_testDirectory);
        var firstFile = Path.Combine(_testDirectory, "first.jpg");
        var secondFile = Path.Combine(_testDirectory, "second.jpg");
        var manager = new TagManager(TagsFilePath);

        manager.AddTags(firstFile, "shared", "first-only");
        manager.AddTags(secondFile, "shared");
        manager.RemoveTags(firstFile, "shared");
        manager.DestroyTag("shared");

        var reloadedManager = new TagManager(TagsFilePath);
        reloadedManager.Load();

        Assert.Equal(new[] { "first-only" }, reloadedManager.GetTagsForFile(firstFile));
        Assert.Empty(reloadedManager.GetTagsForFile(secondFile));
        Assert.Equal(new[] { "first-only" }, reloadedManager.GetAllTags());
    }

    [Fact]
    public void TagLookupsNormalizeCaseAndWhitespaceAcrossIndexes()
    {
        Directory.CreateDirectory(_testDirectory);
        var firstFile = Path.Combine(_testDirectory, "first.jpg");
        var secondFile = Path.Combine(_testDirectory, "second.jpg");
        var manager = new TagManager(TagsFilePath);
        manager.AddTags(firstFile, "Travel");
        manager.AddTags(secondFile, "travel");

        Assert.Equal(2, manager.GetTagFileCount(" TRAVEL "));
        Assert.Equal(
            new[] { firstFile, secondFile }.Select(Path.GetFullPath).Select(path => path.ToLowerInvariant()).OrderBy(path => path),
            manager.GetFilesWithTag("Travel"));
        Assert.Equal(new[] { "travel" }, manager.GetAllTags());
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }
}
