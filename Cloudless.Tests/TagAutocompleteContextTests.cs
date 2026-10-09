using Xunit;

namespace Cloudless.Tests;

public class TagAutocompleteContextTests
{
    [Theory]
    [InlineData("tag add")]
    [InlineData("tag a")]
    [InlineData("tag remove")]
    [InlineData("tag r")]
    [InlineData("tag destroy")]
    [InlineData("t add")]
    [InlineData("t a")]
    [InlineData("t remove")]
    [InlineData("t r")]
    [InlineData("t destroy")]
    public void TagListCommands_ExposeTheCurrentTagToken(string commandBase)
    {
        var text = $"{commandBase} C:\\Images\\sample.jpg tr";

        var isContext = MainWindow.TryGetTagAutocompleteContext(text, out var prefix, out var query);

        Assert.True(isContext);
        Assert.Equal($"{commandBase} C:\\Images\\sample.jpg ", prefix);
        Assert.Equal("tr", query);
    }

    [Theory]
    [InlineData("fs tag")]
    [InlineData("fs t")]
    [InlineData("filmstrip tag")]
    [InlineData("filmstrip t")]
    [InlineData("open tag")]
    [InlineData("open t")]
    [InlineData("o tag")]
    [InlineData("o t")]
    [InlineData("open! tag")]
    [InlineData("open! t")]
    [InlineData("o! tag")]
    [InlineData("o! t")]
    [InlineData("gallery tag")]
    [InlineData("gallery t")]
    public void TagQueryCommands_ExposeTheCurrentExpressionToken(string commandBase)
    {
        var text = $"{commandBase} (travel OR fam";

        var isContext = MainWindow.TryGetTagAutocompleteContext(text, out var prefix, out var query);

        Assert.True(isContext);
        Assert.Equal($"{commandBase} (travel OR ", prefix);
        Assert.Equal("fam", query);
    }

    [Theory]
    [InlineData("fs tag travel AND", "fs tag travel AND ")]
    [InlineData("fs tag travel OR", "fs tag travel OR ")]
    [InlineData("fs tag travel NOT", "fs tag travel NOT ")]
    [InlineData("fs tag travel AND ", "fs tag travel AND ")]
    [InlineData("fs tag ", "fs tag ")]
    [InlineData("gallery t (", "gallery t (")]
    [InlineData("gallery t ( ", "gallery t ( ")]
    public void TagQueryCommands_OfferCompletionAfterOperatorsAndOpenGroups(string text, string expectedPrefix)
    {
        var isContext = MainWindow.TryGetTagAutocompleteContext(text, out var prefix, out var query);

        Assert.True(isContext);
        Assert.Equal(expectedPrefix, prefix);
        Assert.Equal(string.Empty, query);
    }

    [Theory]
    [InlineData("open image.jpg")]
    [InlineData("tag something travel")]
    [InlineData("fs tag travel)")]
    [InlineData("gallery t (travel) ")]
    public void NonTagOrCompletedExpressionContexts_AreNotAutocompleteTargets(string text)
    {
        Assert.False(MainWindow.TryGetTagAutocompleteContext(text, out var prefix, out var query));
        Assert.Equal(string.Empty, prefix);
        Assert.Equal(string.Empty, query);
    }

    [Fact]
    public void TagListCommand_SeparatesTheFileArgumentFromTheCurrentTag()
    {
        var text = "tag add C:\\Images\\sample.jpg travel";

        var isContext = MainWindow.TryGetTagAutocompleteContext(text, out var prefix, out var query);

        Assert.True(isContext);
        Assert.Equal("tag add C:\\Images\\sample.jpg ", prefix);
        Assert.Equal("travel", query);
    }

    [Theory]
    [InlineData("TAG ADD C:\\Images\\sample.jpg TR", "TAG ADD C:\\Images\\sample.jpg ", "TR")]
    [InlineData("Fs TaG travel and fam", "Fs TaG travel and ", "fam")]
    [InlineData("tag add C:\\Images\\sample.jpg\ttr", "tag add C:\\Images\\sample.jpg\t", "tr")]
    public void TagAutocomplete_IsCaseInsensitiveAndRecognizesWhitespaceSeparators(
        string text,
        string expectedPrefix,
        string expectedQuery)
    {
        Assert.True(MainWindow.TryGetTagAutocompleteContext(text, out var prefix, out var query));

        Assert.Equal(expectedPrefix, prefix);
        Assert.Equal(expectedQuery, query);
    }

    [Theory]
    [InlineData("tag added travel")]
    [InlineData("fs tagger travel")]
    public void TagAutocomplete_DoesNotMatchCommandNamesWithExtraCharacters(string text)
    {
        Assert.False(MainWindow.TryGetTagAutocompleteContext(text, out _, out _));
    }
}
