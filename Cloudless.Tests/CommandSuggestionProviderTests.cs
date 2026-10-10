using Xunit;

namespace Cloudless.Tests;

public sealed class CommandSuggestionProviderTests
{
    [Theory]
    [InlineData("cl", "close")]
    [InlineData("close a", "close all")]
    [InlineData("close o", "close others")]
    public void SuggestsDescriptiveValidCommandSpellings(string input, string expected)
    {
        Assert.Equal(expected, Cloudless.ReferenceData.CommandSuggestionProvider.GetSuggestion(input));
    }

    [Fact]
    public void DoesNotSuggestForTextThatIsNotACommandPrefix()
    {
        Assert.Null(Cloudless.ReferenceData.CommandSuggestionProvider.GetSuggestion("asdf"));
    }

    [Fact]
    public void PrefersDescriptiveSpellingsToShortAliases()
    {
        Assert.Equal("filmstrip", Cloudless.ReferenceData.CommandSuggestionProvider.GetSuggestion("f"));
    }

    [Fact]
    public void OffersLiteralOptionsButDoesNotInventParameterValues()
    {
        Assert.Equal("seek ?", Cloudless.ReferenceData.CommandSuggestionProvider.GetSuggestion("seek "));
        Assert.Null(Cloudless.ReferenceData.CommandSuggestionProvider.GetSuggestion("seek 90"));
        Assert.Equal("slideshow", Cloudless.ReferenceData.CommandSuggestionProvider.GetSuggestion("sl"));
    }

    [Theory]
    [InlineData("ws load ")]
    [InlineData("fs preview ")]
    [InlineData("fs tag tr")]
    [InlineData("tag add image.jpg tr")]
    public void SuppressesSuggestionsInExistingTabbableParameterContexts(string input)
    {
        Assert.Null(MainWindow.GetCommandCompletion(input, input.Length));
    }

    [Fact]
    public void OnlySuggestsWhenCaretIsAtEndOfInput()
    {
        Assert.Null(MainWindow.GetCommandCompletion("cl", 1));
        Assert.Equal("close", MainWindow.GetCommandCompletion("cl", 2));
    }

    [Theory]
    [InlineData("all cl", "all close")]
    [InlineData("others cl", "others close")]
    [InlineData("c3 set cl", "c3 set close")]
    public void SuggestsCommandsForCommandValuedParameters(string input, string expected)
    {
        Assert.Equal(expected, MainWindow.GetCommandCompletion(input, input.Length));
    }

    [Fact]
    public void DoesNotSuggestCommandValuesForOtherCustomCommandParameters()
    {
        Assert.Null(MainWindow.GetCommandCompletion("c3 set ", "c3 set ".Length));
        Assert.Null(MainWindow.GetCommandCompletion("all ws load ", "all ws load ".Length));
    }

    [Fact]
    public void ReturnsMultipleCommandSuggestionsInPreferredOrder()
    {
        Assert.Equal(
            new[] { "close all", "close others", "close empty" },
            MainWindow.GetCommandCompletions("close ", "close ".Length));
    }

    [Fact]
    public void SuggestsFilmstripSourcesAfterAcceptingTheCommand()
    {
        Assert.Equal("filmstrip", MainWindow.GetCommandCompletion("fil", 3));
        Assert.Equal("filmstrip directory", MainWindow.GetCommandCompletion("filmstrip", "filmstrip".Length));
        Assert.Equal(
            new[] { "filmstrip directory", "filmstrip recent", "filmstrip bookmarks", "filmstrip preview" },
            MainWindow.GetCommandCompletions("filmstrip ", "filmstrip ".Length).Take(4));
    }

    [Theory]
    [InlineData("nudge", "nudge left")]
    [InlineData("nudge r", "nudge right")]
    [InlineData("sort", "sort name")]
    [InlineData("sort date", "sort date asc")]
    [InlineData("sort name d", "sort name desc")]
    [InlineData("dm b", "dm best")]
    [InlineData("ris g", "ris google")]
    [InlineData("slideshow ", "slideshow stop")]
    [InlineData("tag ", "tag add")]
    public void OffersValidLiteralCommandOptions(string input, string expected)
    {
        Assert.Equal(expected, MainWindow.GetCommandCompletion(input, input.Length));
    }

    [Fact]
    public void SuggestsFullSlideshowSpellingsAfterDuration()
    {
        Assert.Contains("slideshow 5 triggers", MainWindow.GetCommandCompletions("slideshow 5", "slideshow 5".Length));
        Assert.DoesNotContain("slideshow 5 shuffle triggers", MainWindow.GetCommandCompletions("slideshow 5", "slideshow 5".Length));
        Assert.Equal("slideshow 5 triggers", MainWindow.GetCommandCompletion("slideshow 5 tri", "slideshow 5 tri".Length));
        Assert.Equal("slideshow 5 shuffle", MainWindow.GetCommandCompletion("slideshow 5 sh", "slideshow 5 sh".Length));
        Assert.Equal("ss shuffle", MainWindow.GetCommandCompletion("ss ", "ss ".Length));
        Assert.Equal("ss t shuffle", MainWindow.GetCommandCompletion("ss t ", "ss t ".Length));
        Assert.Equal("slideshow t shuffle", MainWindow.GetCommandCompletion("slideshow t ", "slideshow t ".Length));
        Assert.Equal("ss 5 s triggers", MainWindow.GetCommandCompletion("ss 5 s ", "ss 5 s ".Length));
        Assert.Equal("ss shuffle triggers", MainWindow.GetCommandCompletion("ss shuffle ", "ss shuffle ".Length));
        Assert.Equal("slideshow 5 shuffle triggers", MainWindow.GetCommandCompletion("slideshow 5 shuffle ", "slideshow 5 shuffle ".Length));
        Assert.Equal("ss next", MainWindow.GetCommandCompletion("ss n", "ss n".Length));
        Assert.Contains("ss 5 shuffle", MainWindow.GetCommandCompletions("ss 5", "ss 5".Length));
    }

    [Theory]
    [InlineData("go", "goto")]
    [InlineData("goto", "goto start")]
    [InlineData("goto ", "goto start")]
    [InlineData("sort", "sort name")]
    [InlineData("sort name", "sort name asc")]
    public void OffersEachWordOfAtomicMultiwordCommandsAsASeparateCompletion(string input, string expected)
    {
        Assert.Equal(expected, MainWindow.GetCommandCompletion(input, input.Length));
    }

    [Theory]
    [InlineData("p 2", "p 2 send")]
    [InlineData("p 2 s", "p 2 send")]
    [InlineData("p 2 send", "p 2 send page")]
    [InlineData("slideshow 5", "slideshow 5 shuffle")]
    [InlineData("slideshow 5 t", "slideshow 5 triggers")]
    [InlineData("macro record demo cl", "macro record demo close")]
    [InlineData("close; fil", "close; filmstrip")]
    public void OffersTheNextLiteralAfterCommandAndNumericParameters(string input, string expected)
    {
        Assert.Equal(expected, MainWindow.GetCommandCompletion(input, input.Length));
    }

    [Fact]
    public void DoesNotOfferInvalidSlideshowContinuationForNonPositiveDuration()
    {
        Assert.Null(MainWindow.GetCommandCompletion("slideshow 0", "slideshow 0".Length));
    }
}
