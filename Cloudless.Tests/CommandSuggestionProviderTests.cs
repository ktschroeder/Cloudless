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
    public void PrefersHotkeyOverItsShortAliasAndDoesNotSuggestMalformedPageCommands()
    {
        var suggestions = Cloudless.ReferenceData.CommandSuggestionProvider.GetSuggestions("h");
        Assert.Equal("hotkey", suggestions.First());
        Assert.DoesNotContain("hk", suggestions);

        Assert.Contains("page", Cloudless.ReferenceData.CommandSuggestionProvider.GetSuggestions("p"));
        Assert.Equal("fs workspace", MainWindow.GetCommandCompletion("fs w", "fs w".Length));

        var slideshowSuggestions = MainWindow.GetCommandCompletions("s", 1);
        Assert.Contains("slideshow", slideshowSuggestions);
        Assert.DoesNotContain("ss", slideshowSuggestions);

        var videoSlideshowSuggestions = MainWindow.GetCommandCompletions("set s", "set s".Length);
        Assert.Contains("set slideshow", videoSlideshowSuggestions);
        Assert.DoesNotContain("set ss", videoSlideshowSuggestions);

        var bSuggestions = Cloudless.ReferenceData.CommandSuggestionProvider.GetSuggestions("b");
        Assert.DoesNotContain("b page", bSuggestions);
    }

    [Fact]
    public void DoesNotSuggestNumericPageTargetsButStillAcceptsThem()
    {
        var suggestions = Cloudless.ReferenceData.CommandSuggestionProvider.GetSuggestions("p ");

        Assert.DoesNotContain(suggestions, suggestion =>
            int.TryParse(suggestion.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last(), out _));
        Assert.Contains("p this", suggestions);
        Assert.DoesNotContain("p t", suggestions);
        Assert.Contains("p previous", suggestions);
        Assert.DoesNotContain("p prev", suggestions);
        Assert.Contains("p next", suggestions);
        Assert.DoesNotContain("p n", suggestions);
        Assert.True(CommandSyntaxValidator.IsValidCommand("p 2"));
    }

    [Fact]
    public void ReferenceRowsRemainCuratedAndIndependentOfCatalogForms()
    {
        var tabs = Cloudless.ReferenceData.CommandReferenceData.GetTabs();
        var tagItems = tabs.Single(tab => tab.Header == "Tags").Items;
        var advancedItems = tabs.Single(tab => tab.Header == "Advanced").Items;

        Assert.Contains(tagItems, item => item.Key == "filmstrip | fs tag | t [query]");
        Assert.Contains(tagItems, item => item.Key == "open | o tag | t [query]");
        Assert.Contains(advancedItems, item => item.Key == "hotkey | hk [modifiers] [key]");
    }

    [Fact]
    public void CompletesVideoPositionCommandsFromTheirCatalogGrammar()
    {
        Assert.Equal("set start", MainWindow.GetCommandCompletion("set s", "set s".Length));
        Assert.Equal("clear flag", MainWindow.GetCommandCompletion("clear f", "clear f".Length));
        Assert.Equal("goto start", MainWindow.GetCommandCompletion("goto ", "goto ".Length));
        Assert.Null(MainWindow.GetCommandCompletion("set trigger ", "set trigger ".Length));
    }

    [Fact]
    public void SuggestsReverseSearchServicesFromTheCatalogGrammar()
    {
        Assert.Equal("ris google", MainWindow.GetCommandCompletion("ris g", "ris g".Length));
        Assert.Equal("ris google", MainWindow.GetCommandCompletion("ris ", "ris ".Length));
        Assert.Null(MainWindow.GetCommandCompletion("ris invalid", "ris invalid".Length));
    }

    [Fact]
    public void SuggestsTheNextVideoCommandLiteralWithoutInventingNumericValues()
    {
        Assert.Equal("audio sync", MainWindow.GetCommandCompletion("audio", "audio".Length));
        Assert.Equal("subtitle sync", MainWindow.GetCommandCompletion("subtitle ", "subtitle ".Length));
        Assert.Equal("seek ?", MainWindow.GetCommandCompletion("seek ", "seek ".Length));
        Assert.Null(MainWindow.GetCommandCompletion("volume ", "volume ".Length));
        Assert.Null(MainWindow.GetCommandCompletion("audio sync ", "audio sync ".Length));
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
    [InlineData("macro record demo cl", "macro record demo close")]
    [InlineData("close; cl", "close; close")]
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
            new[] { "close all", "close others", "close sel", "close empty" },
            MainWindow.GetCommandCompletions("close ", "close ".Length));
    }

    [Fact]
    public void SuggestsFilmstripSourcesAfterAcceptingTheCommand()
    {
        Assert.Equal("filmstrip", MainWindow.GetCommandCompletion("fil", 3));
        Assert.Equal("filmstrip directory", MainWindow.GetCommandCompletion("filmstrip", "filmstrip".Length));
        Assert.Equal(
            new[] { "filmstrip directory", "filmstrip recent", "filmstrip bookmarks", "filmstrip workspace" },
            MainWindow.GetCommandCompletions("filmstrip ", "filmstrip ".Length).Take(4));
        var shortAliasSuggestions = MainWindow.GetCommandCompletions("fs ", "fs ".Length);
        Assert.Contains("fs bookmarks", shortAliasSuggestions);
        Assert.DoesNotContain("fs bookmark", shortAliasSuggestions);
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

    [Theory]
    [InlineData(@"gallery d C:\Users\Admin\Downloads\delete me\oct 12")]
    [InlineData(@"gallery directory C:\Users\Admin\Downloads\delete me\oct 12\image.jpg")]
    [InlineData("gallery d")]
    [InlineData(@"g d C:\Users\Admin\Downloads\delete me\oct 12")]
    [InlineData("g d")]
    [InlineData(@"g tag sample")]
    public void AcceptsGalleryAliasesAndPathsContainingSpaces(string command)
    {
        Assert.True(CommandSyntaxValidator.IsValidCommand(command));
    }

    [Fact]
    public void SuggestsFullSlideshowSpellingsAfterDuration()
    {
        Assert.Contains("slideshow 5 shuffle", MainWindow.GetCommandCompletions("slideshow 5", "slideshow 5".Length));
        Assert.DoesNotContain("slideshow 5 triggers", MainWindow.GetCommandCompletions("slideshow 5", "slideshow 5".Length));
        Assert.DoesNotContain("slideshow 5 shuffle triggers", MainWindow.GetCommandCompletions("slideshow 5", "slideshow 5".Length));
        Assert.Null(MainWindow.GetCommandCompletion("slideshow 5 tri", "slideshow 5 tri".Length));
        Assert.Equal("slideshow 5 shuffle", MainWindow.GetCommandCompletion("slideshow 5 sh", "slideshow 5 sh".Length));
        Assert.Equal("ss shuffle", MainWindow.GetCommandCompletion("ss ", "ss ".Length));
        Assert.Equal("ss t shuffle", MainWindow.GetCommandCompletion("ss t ", "ss t ".Length));
        Assert.Equal("slideshow t shuffle", MainWindow.GetCommandCompletion("slideshow t ", "slideshow t ".Length));
        Assert.Null(MainWindow.GetCommandCompletion("ss 5 s ", "ss 5 s ".Length));
        Assert.Equal("ss shuffle triggers", MainWindow.GetCommandCompletion("ss shuffle ", "ss shuffle ".Length));
        Assert.Null(MainWindow.GetCommandCompletion("slideshow 5 shuffle ", "slideshow 5 shuffle ".Length));
        Assert.Equal("ss next", MainWindow.GetCommandCompletion("ss n", "ss n".Length));
        Assert.Equal(
            new[] { "ss shuffle", "ss status", "ss stop" },
            MainWindow.GetCommandCompletions("ss s", "ss s".Length));
        Assert.Equal(
            new[] { "ss status", "ss stop" },
            MainWindow.GetCommandCompletions("ss st", "ss st".Length));
        Assert.Equal("ss pause", MainWindow.GetCommandCompletion("ss p", "ss p".Length));
        Assert.Equal("ss resume", MainWindow.GetCommandCompletion("ss r", "ss r".Length));
        Assert.Contains("ss 5 shuffle", MainWindow.GetCommandCompletions("ss 5", "ss 5".Length));
        Assert.Contains("ss pause", MainWindow.GetCommandCompletions("ss ", "ss ".Length));
        Assert.Contains("ss resume", MainWindow.GetCommandCompletions("ss ", "ss ".Length));
        Assert.Contains("ss status", MainWindow.GetCommandCompletions("ss ", "ss ".Length));
        Assert.Contains("slideshow pause", MainWindow.GetCommandCompletions("slideshow ", "slideshow ".Length));
        Assert.Contains("slideshow resume", MainWindow.GetCommandCompletions("slideshow ", "slideshow ".Length));
        Assert.Contains("slideshow status", MainWindow.GetCommandCompletions("slideshow ", "slideshow ".Length));
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
