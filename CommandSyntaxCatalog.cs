using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Windows.Input;

namespace Cloudless;

internal static class CommandSyntaxCatalog
{
    private static readonly CommandToken HotkeyRoot = CommandToken.Literal("command", "hotkey", "hk");
    private static readonly CommandToken PageRoot = CommandToken.Literal("command", "p", "page");
    private static readonly LiteralValue[] PageTargetSuggestions =
    {
        new("this", "t"),
        new("previous", "p", "prev"),
        new("next", "n"),
        new("pa"),
        new("na"),
        new("pi"),
        new("ni")
    };
    private static readonly CommandToken HotkeyModifier = CommandToken.Literal(
        "modifier",
        displayText: "[modifiers]",
        new LiteralValue("ctrl", "control"),
        new LiteralValue("alt"),
        new LiteralValue("shift"),
        repeatable: true);
    private static readonly CommandToken HotkeyKey = CommandToken.Argument("key", IsValidHotkeyKey);
    private static readonly CommandToken PageTarget = CommandToken.ArgumentWithAliases(
        "target", IsPageTarget, PageTargetSuggestions);
    private static readonly CommandToken PageSecondTarget = CommandToken.Argument(
        "secondTarget",
        IsPageTarget,
        PageTarget.SuggestedValues.ToArray());
    private static readonly CommandToken SendAction = CommandToken.Literal("action", "send", "s");
    private static readonly CommandToken BringAction = CommandToken.Literal("action", "bring", "b");
    private static readonly CommandToken ClearAction = CommandToken.Literal("action", "clear", "c");
    private static readonly CommandToken SwapAction = CommandToken.Literal("action", "swap", "x");
    private static readonly CommandToken PageQualifier = CommandToken.Literal("qualifier", "page", "p");
    private static readonly CommandToken PageStatus = CommandToken.Literal("status", "?");
    private static readonly CommandToken PageArrange = CommandToken.Literal("operation", "arrange");
    private static readonly CommandToken PositionSetRoot = CommandToken.Literal("command", "set");
    private static readonly CommandToken PositionClearRoot = CommandToken.Literal("command", "clear");
    private static readonly CommandToken PositionGotoRoot = CommandToken.Literal("command", "goto");
    private static readonly CommandToken PositionStart = CommandToken.Literal("position", "start", "s");
    private static readonly CommandToken PositionEnd = CommandToken.Literal("position", "end", "e");
    private static readonly CommandToken PositionFlag = CommandToken.Literal("position", "flag", "f");
    private static readonly CommandToken TriggerRoot = CommandToken.Literal("trigger", "trigger", "t");
    private static readonly CommandToken TriggerCount = CommandToken.Argument("count", IsPositiveInteger);
    private static readonly CommandToken SortRoot = CommandToken.Literal("command", "sort");
    private static readonly CommandToken SortName = CommandToken.Literal("type", "name");
    private static readonly CommandToken SortDate = CommandToken.Literal("type", "date");
    private static readonly CommandToken SortAscending = CommandToken.Literal("order", "asc");
    private static readonly CommandToken SortDescending = CommandToken.Literal("order", "desc");
    private static readonly CommandToken DisplayModeRoot = CommandToken.Literal("command", "dm");
    private static readonly CommandToken DisplayStretch = CommandToken.Literal("mode", "stretch", "1");
    private static readonly CommandToken DisplayZoom = CommandToken.Literal("mode", "zoom", "2");
    private static readonly CommandToken DisplayBest = CommandToken.Literal("mode", "best", "3");
    private static readonly CommandToken DisplayBestNoZoom = CommandToken.Literal("mode", "bestnozoom", "4");
    private static readonly CommandToken ReverseSearchRoot = CommandToken.Literal("command", "ris");
    private static readonly CommandToken ReverseSearchService = CommandToken.Argument(
        "service",
        IsValidReverseSearchService,
        "google", "bing", "yandex", "tineye", "saucenao");
    private static readonly CommandToken NudgeRoot = CommandToken.Literal("command", "nudge", "n");
    private static readonly CommandToken NudgeDirection = CommandToken.LiteralOptions(
        "direction",
        "left/l|right/r|up/u|down/d",
        new LiteralValue("left", "l"),
        new LiteralValue("right", "r"),
        new LiteralValue("up", "u"),
        new LiteralValue("down", "d"));
    private static readonly CommandToken NudgeCount = CommandToken.Argument("count", IsInteger);
    private static readonly CommandToken NudgeX = CommandToken.Argument("x", IsInteger);
    private static readonly CommandToken NudgeY = CommandToken.Argument("y", IsInteger);
    private static readonly CommandToken MuteRoot = CommandToken.Literal("command", "mute");
    private static readonly CommandToken UnmuteRoot = CommandToken.Literal("command", "unmute");
    private static readonly CommandToken VolumeRoot = CommandToken.Literal("command", "volume");
    private static readonly CommandToken VolumeValue = CommandToken.Argument("number", IsValidVolume);
    private static readonly CommandToken SpeedRoot = CommandToken.Literal("command", "speed");
    private static readonly CommandToken SpeedValue = CommandToken.Argument("multiplier", IsValidPlaybackSpeed);
    private static readonly CommandToken AudioRoot = CommandToken.Literal("command", "audio");
    private static readonly CommandToken AudioSync = CommandToken.Literal("action", "sync");
    private static readonly CommandToken AudioOffset = CommandToken.Argument("offset", IsValidVideoDelay);
    private static readonly CommandToken SubtitleRoot = CommandToken.Literal("command", "subtitle");
    private static readonly CommandToken PlayRoot = CommandToken.Literal("command", "play");
    private static readonly CommandToken PauseRoot = CommandToken.Literal("command", "pause");
    private static readonly CommandToken SyncRoot = CommandToken.Literal("command", "sync");
    private static readonly CommandToken UnsyncRoot = CommandToken.Literal("command", "unsync");
    private static readonly CommandToken SeekRoot = CommandToken.Literal("command", "seek");
    private static readonly CommandToken SeekStatus = CommandToken.Literal("option", "?");
    private static readonly CommandToken SeekPrevious = CommandToken.Literal("option", "previous", "p");
    private static readonly CommandToken SeekAbsoluteTime = CommandToken.Argument("time", IsValidAbsoluteSeekTime, displayText: "[absolute time]");
    private static readonly CommandToken SeekForwardTime = CommandToken.Argument("time", IsValidForwardSeekTime, displayText: "+[relative time]");
    private static readonly CommandToken SeekBackwardTime = CommandToken.Argument("time", IsValidBackwardSeekTime, displayText: "-[relative time]");
    private static readonly CommandToken FilmstripRoot = CommandToken.Literal("command", "filmstrip", "fs");
    private static readonly CommandToken FilmstripDirectory = CommandToken.Literal("source", "directory", "d");
    private static readonly CommandToken FilmstripDirectoryPath = CommandToken.ArgumentRest("path", value => !string.IsNullOrWhiteSpace(value));
    private static readonly CommandToken FilmstripRecent = CommandToken.Literal("source", "recent", "r");
    private static readonly CommandToken FilmstripBookmarks = CommandToken.Literal("source", "bookmarks", "b");
    private static readonly CommandToken FilmstripPreview = CommandToken.Literal("source", "preview", "p", "workspace", "ws");
    private static readonly CommandToken FilmstripCurrentAlias = CommandToken.Literal("source", "fs", "filmstrip");
    private static readonly CommandToken FilmstripWorkspace = CommandToken.ArgumentRest("workspace", value => !string.IsNullOrWhiteSpace(value));
    private static readonly CommandToken TagQueryRoot = CommandToken.Literal("action", "tag", "t");
    private static readonly CommandToken TagQuery = CommandToken.ArgumentRest("query", value => !string.IsNullOrWhiteSpace(value));
    private static readonly CommandToken GalleryRoot = CommandToken.Literal("command", "gallery", "g");
    private static readonly CommandToken GalleryDirectory = CommandToken.Literal("source", "directory", "d");
    private static readonly CommandToken GalleryDirectoryPath = CommandToken.ArgumentRest("path", value => !string.IsNullOrWhiteSpace(value));
    private static readonly CommandToken DimensionsRoot = CommandToken.Literal("command", "dim");
    private static readonly CommandToken DimensionWidth = CommandToken.Argument("width", IsDimensionValue);
    private static readonly CommandToken DimensionHeight = CommandToken.Argument("height", IsDimensionValue);
    private static readonly CommandToken WorkspaceRoot = CommandToken.Literal("command", "ws");
    private static readonly CommandToken WorkspaceSave = CommandToken.Literal("action", "save", "s");
    private static readonly CommandToken WorkspaceSaveForce = CommandToken.Literal("action", "save!", "s!");
    private static readonly CommandToken WorkspaceLoad = CommandToken.Literal("action", "load", "l");
    private static readonly CommandToken WorkspaceMerge = CommandToken.Literal("action", "merge", "m");
    private static readonly CommandToken WorkspaceDelete = CommandToken.Literal("action", "delete");
    private static readonly CommandToken WorkspaceRename = CommandToken.Literal("action", "rename", "r");
    private static readonly CommandToken WorkspacePreview = CommandToken.Literal("action", "preview", "p");
    private static readonly CommandToken WorkspaceOrigin = CommandToken.Literal("action", "origin");
    private static readonly CommandToken WorkspaceOriginSave = CommandToken.Literal("action", "save", "s", "save!", "s!");
    private static readonly CommandToken WorkspaceOriginLoad = CommandToken.Literal("action", "load", "l");
    private static readonly CommandToken WorkspaceList = CommandToken.Literal("action", "list");
    private static readonly CommandToken WorkspaceRev = CommandToken.Literal("action", "rev");
    private static readonly CommandToken WorkspaceUndoload = CommandToken.Literal("action", "undoload");
    private static readonly CommandToken WorkspaceUndocloseall = CommandToken.Literal("action", "undocloseall");
    private static readonly CommandToken WorkspaceName = CommandToken.ArgumentRest("workspace", value => !string.IsNullOrWhiteSpace(value));
    private static readonly CommandToken WorkspacePath = CommandToken.ArgumentRest("path", value => !string.IsNullOrWhiteSpace(value));
    private static readonly CommandToken WorkspaceOldName = CommandToken.Argument("oldName", value => !string.IsNullOrWhiteSpace(value));
    private static readonly CommandToken WorkspaceNewName = CommandToken.Argument("newName", value => !string.IsNullOrWhiteSpace(value));
    private static readonly CommandToken QuickSaveRoot = CommandToken.Literal("command", "qs");
    private static readonly CommandToken QuickSaveClose = CommandToken.Literal("action", "c");
    private static readonly CommandToken QuickLoadRoot = CommandToken.Literal("command", "ql");
    private static readonly CommandToken QuickMergeRoot = CommandToken.Literal("command", "qm");
    private static readonly CommandToken SelectionCountRoot = CommandToken.Literal("command", "sel");
    private static readonly CommandToken SelectedAction = CommandToken.Literal("scope", "sel");
    private static readonly CommandToken SelectRoot = CommandToken.Literal("command", "select");
    private static readonly CommandToken DeselectRoot = CommandToken.Literal("command", "deselect");
    private static readonly CommandToken PreventOverlapRoot = CommandToken.Literal("command", "prevent");
    private static readonly CommandToken AllowOverlapRoot = CommandToken.Literal("command", "allow");
    private static readonly CommandToken PreventOverlapAliasRoot = CommandToken.LiteralWithSuggestionAlias(
        "command", "po", new LiteralValue("prevent"));
    private static readonly CommandToken AllowOverlapAliasRoot = CommandToken.LiteralWithSuggestionAlias(
        "command", "ao", new LiteralValue("allow"));
    private static readonly CommandToken OverlapWord = CommandToken.Literal("option", "overlap");
    private static readonly CommandToken ClearSelectionRoot = CommandToken.Literal("command", "clear");
    private static readonly CommandToken ClearSelectionAliasRoot = CommandToken.Literal("command", "cs");
    private static readonly CommandToken InvertSelectionRoot = CommandToken.Literal("command", "invert");
    private static readonly CommandToken InvertSelectionAliasRoot = CommandToken.Literal("command", "is");
    private static readonly CommandToken CloseRoot = CommandToken.Literal("command", "close", "c");
    private static readonly CommandToken CloseOriginRoot = CommandToken.Literal("command", "c");
    private static readonly CommandToken MinimizeRoot = CommandToken.Literal("command", "m");
    private static readonly CommandToken UnminimizeRoot = CommandToken.Literal("command", "um");
    private static readonly CommandToken AllAction = CommandToken.Literal("scope", "all");
    private static readonly CommandToken OthersAction = CommandToken.Literal("scope", "others");
    private static readonly CommandToken EmptyAction = CommandToken.Literal("scope", "empty");
    private static readonly CommandToken OriginAction = CommandToken.Literal("scope", "origin");
    private static readonly CommandToken ShutdownRoot = CommandToken.Literal("command", "shutdown", "sd");
    private static readonly CommandToken DeflashRoot = CommandToken.Literal("command", "deflash", "df");
    private static readonly CommandToken StackRoot = CommandToken.Literal("command", "stack");
    private static readonly CommandToken FlattenRoot = CommandToken.Literal("command", "flatten");
    private static readonly CommandToken CopyImagePathRoot = CommandToken.Literal("command", "cip");
    private static readonly CommandToken RevealImageRoot = CommandToken.Literal("command", "rev");
    private static readonly CommandToken HelpRoot = CommandToken.Literal("command", "help", "h");
    private static readonly CommandToken SlideshowLongRoot = CommandToken.Literal("command", "slideshow");
    private static readonly CommandToken SlideshowShortRoot = CommandToken.LiteralWithSuggestionAlias(
        "command", "ss", new LiteralValue("slideshow", "ss"));
    private static readonly CommandToken SlideshowShuffle = CommandToken.Literal("option", "shuffle");
    private static readonly CommandToken SlideshowTriggers = CommandToken.Literal("option", "triggers", "t");
    private static readonly CommandToken SlideshowStop = CommandToken.Literal("action", "stop");
    private static readonly CommandToken SlideshowStopShort = CommandToken.Literal("action", "stop", "s");
    private static readonly CommandToken SlideshowNext = CommandToken.Literal("action", "next", "n");
    private static readonly CommandToken SlideshowPause = CommandToken.Literal("action", "pause");
    private static readonly CommandToken SlideshowPauseShort = CommandToken.Literal("action", "pause", "p");
    private static readonly CommandToken SlideshowResume = CommandToken.Literal("action", "resume");
    private static readonly CommandToken SlideshowResumeShort = CommandToken.Literal("action", "resume", "r");
    private static readonly CommandToken SlideshowStatus = CommandToken.Literal("action", "status");
    private static readonly CommandToken SlideshowInterval = CommandToken.Argument("seconds", IsPositiveSeconds);
    private static readonly CommandToken TagRoot = CommandToken.Literal("command", "tag", "t");
    private static readonly CommandToken TagAdd = CommandToken.Literal("action", "add", "a");
    private static readonly CommandToken TagRemove = CommandToken.Literal("action", "remove", "r");
    private static readonly CommandToken TagDestroy = CommandToken.Literal("action", "destroy");
    private static readonly CommandToken TagList = CommandToken.Literal("action", "list", "l");
    private static readonly CommandToken TagNames = CommandToken.ArgumentRest("tags", value => !string.IsNullOrWhiteSpace(value));
    private static readonly CommandToken FirstRoot = CommandToken.Literal("command", "first");
    private static readonly CommandToken LastRoot = CommandToken.Literal("command", "last");
    private static readonly CommandToken RecentRoot = CommandToken.Literal("command", "r");
    private static readonly CommandToken RecentCount = CommandToken.Argument("count", IsPositiveInteger);
    private static readonly CommandToken AbsoluteImageIndex = CommandToken.Argument("index", IsNonnegativeInteger, displayText: "[Int]");
    private static readonly CommandToken ForwardImageOffset = CommandToken.Argument("offset", IsPositiveOffset, displayText: "+[Int]");
    private static readonly CommandToken BackwardImageOffset = CommandToken.Argument("offset", IsNegativeOffset, displayText: "-[Int]");
    private static readonly CommandToken FilenameSearchQuery = CommandToken.ArgumentRest(
        "query",
        value => value.Length > 1 && value[0] == '/',
        displayText: "/[query]");
    private static readonly CommandToken OpenPathRoot = CommandToken.Literal("command", "open", "o");
    private static readonly CommandToken OpenAllPathRoot = CommandToken.Literal("command", "open!", "o!");
    private static readonly CommandToken OpenPath = CommandToken.ArgumentRest("path", value => !string.IsNullOrWhiteSpace(value));
    private static readonly CommandToken MacroRoot = CommandToken.Literal("command", "macro");
    private static readonly CommandToken MacroRecord = CommandToken.Literal("action", "record");
    private static readonly CommandToken MacroRun = CommandToken.Literal("action", "run");
    private static readonly CommandToken MacroDelete = CommandToken.Literal("action", "delete");
    private static readonly CommandToken MacroList = CommandToken.Literal("action", "list");
    private static readonly CommandToken MacroName = CommandToken.ArgumentRest("name", value => !string.IsNullOrWhiteSpace(value));
    private static readonly CommandToken MacroRecordName = CommandToken.Argument("name", value => !string.IsNullOrWhiteSpace(value));
    private static readonly CommandToken MacroCommand = CommandToken.ArgumentRest("macroCommand", value => !string.IsNullOrWhiteSpace(value), displayText: "[command]");
    private static readonly CommandToken CustomCommandRoot = CommandToken.Argument("customIndex", IsValidCustomCommandIndex, "c[Int 1-24]");
    private static readonly CommandToken CustomCommandSet = CommandToken.Literal("action", "set");
    private static readonly CommandToken CustomCommandView = CommandToken.Literal("action", "view");
    private static readonly CommandToken CustomCommandRun = CommandToken.Literal("action", "run");
    private static readonly CommandToken CustomCommandText = CommandToken.ArgumentRest("commandText", value => !string.IsNullOrWhiteSpace(value), displayText: "[command]");
    private static readonly CommandToken AllWrapperRoot = CommandToken.Literal("wrapper", "all");
    private static readonly CommandToken OthersWrapperRoot = CommandToken.Literal("wrapper", "others");
    private static readonly CommandToken SelectedWrapperRoot = CommandToken.Literal("wrapper", "sel");
    private static readonly CommandToken WrappedCommand = CommandToken.ArgumentRest("wrappedCommand", value => !string.IsNullOrWhiteSpace(value), displayText: "[command]");
    private static readonly CommandDefinition[] Definitions = CreateDefinitions();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScan(char character);

    internal static CommandParseResult Parse(string input)
    {
        if (input.Contains(';'))
        {
            CommandParseResult wholeCommand = ParseSingle(input);
            if (wholeCommand.DefinitionId == "macros" &&
                wholeCommand.Operation == "macro-record" &&
                wholeCommand.IsComplete)
                return wholeCommand;

            string[] commands = SplitCommandSequence(input);
            return CommandParseResult.CreateSequence(commands, commands.Select(ParseSingle).ToArray());
        }

        return ParseSingle(input);
    }

    internal static string[] SplitCommandSequence(string input) =>
        input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal static bool TryGetCommandSequenceCompletionContext(string input, out string prefix, out string command)
    {
        int lastSemicolon = input.LastIndexOf(';');
        if (lastSemicolon < 0)
        {
            prefix = string.Empty;
            command = string.Empty;
            return false;
        }

        string segment = input[(lastSemicolon + 1)..];
        int leadingWhitespace = segment.Length - segment.TrimStart().Length;
        prefix = input[..(lastSemicolon + 1)] + segment[..leadingWhitespace];
        command = segment[leadingWhitespace..];
        return true;
    }

    internal static IReadOnlyList<string> GetTagListAutocompleteCommandBases() =>
        GetCommandBases(Definitions
            .Where(definition => definition.Id == "tags")
            .SelectMany(definition => definition.Forms)
            .Where(form => form.Operation is "tag-add" or "tag-remove" or "tag-destroy"));

    internal static IReadOnlyList<string> GetTagQueryAutocompleteCommandBases() =>
        GetCommandBases(Definitions
            .SelectMany(definition => definition.Forms)
            .Where(form => form.Operation.EndsWith("tag-query", StringComparison.Ordinal)));

    private static IReadOnlyList<string> GetCommandBases(IEnumerable<CommandForm> forms) =>
        forms.SelectMany(form =>
                from root in form.Tokens[0].GetLiteralValues()
                from action in form.Tokens[1].GetLiteralValues()
                select $"{root} {action}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static bool TryGetCommandValueContext(string input, out string prefix, out string command)
    {
        prefix = string.Empty;
        command = string.Empty;
        string[] tokens = Tokenize(input);
        MatchCollection matches = Regex.Matches(input, @"\S+");
        if (tokens.Length != matches.Count || tokens.Length < 2)
            return false;

        int consumedTokens;
        if (AllWrapperRoot.TryMatch(tokens[0], out _) || OthersWrapperRoot.TryMatch(tokens[0], out _) || SelectedWrapperRoot.TryMatch(tokens[0], out _))
            consumedTokens = 1;
        else if (MacroRoot.TryMatch(tokens[0], out _) && tokens.Length >= 4 && MacroRecord.TryMatch(tokens[1], out _))
            consumedTokens = 3;
        else if (CustomCommandRoot.TryMatch(tokens[0], out _) && CustomCommandSet.TryMatch(tokens[1], out _))
            consumedTokens = 2;
        else
            return false;

        int argumentStart = matches[consumedTokens - 1].Index + matches[consumedTokens - 1].Length;
        while (argumentStart < input.Length && char.IsWhiteSpace(input[argumentStart]))
            argumentStart++;
        if (argumentStart >= input.Length)
            return false;

        prefix = input[..argumentStart];
        command = input[argumentStart..];
        return true;
    }

    private static CommandParseResult ParseSingle(string input)
    {
        string[] tokens = Tokenize(input);
        if (tokens.Length == 0)
            return CommandParseResult.Unrecognized;

        foreach (CommandDefinition definition in Definitions)
        {
            if (!definition.Forms.Any(form => form.Tokens[0].TryMatch(tokens[0], out _)))
                continue;

            foreach (CommandForm form in definition.Forms)
            {
                if (definition.Id == "open-path" && IsOpenTagQueryRoot(tokens) &&
                    form.Operation is "open-path" or "open-all-path")
                    continue;

                if (TryMatchComplete(form, tokens, out var arguments))
                {
                    if (definition.Id == "window-dimensions" &&
                        arguments.TryGetValue("width", out List<string>? widths) &&
                        arguments.TryGetValue("height", out List<string>? heights) &&
                        widths.FirstOrDefault() == "_" && heights.FirstOrDefault() == "_")
                    {
                        return new CommandParseResult(
                            definition.Id,
                            operation: null,
                            arguments,
                            CommandParseStatus.Invalid,
                            GetDimensionUsageMessage());
                    }

                    return new CommandParseResult(definition.Id, form.Operation, arguments, CommandParseStatus.Complete);
                }
            }

            bool isIncomplete = definition.Forms.Any(form => MatchesPrefix(form, tokens, 0, 0));
            string? errorMessage = definition.Id switch
            {
                "hotkey" => GetHotkeyErrorMessage(tokens, isIncomplete),
                "video-position" => GetVideoPositionErrorMessage(tokens),
                "reverse-image-search" => GetReverseImageSearchErrorMessage(tokens),
                "window-nudge" => GetNudgeErrorMessage(tokens),
                "video-transport" => GetVideoTransportErrorMessage(tokens),
                "filmstrip" => GetFilmstripErrorMessage(tokens),
                "window-dimensions" => GetDimensionUsageMessage(),
                "workspace" => GetWorkspaceErrorMessage(tokens),
                "custom-commands" => GetCustomCommandErrorMessage(tokens),
                "command-wrappers" => $"Usage: {tokens[0]} [command]",
                "open-path" => GetOpenPathErrorMessage(tokens),
                _ => definition.Usage
            };

            return new CommandParseResult(
                definition.Id,
                operation: null,
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
                isIncomplete ? CommandParseStatus.Incomplete : CommandParseStatus.Invalid,
                errorMessage);
        }

        return CommandParseResult.Unrecognized;
    }

    internal static IReadOnlyList<string> GetSuggestions(string input, out bool handlesInput)
    {
        handlesInput = false;
        if (string.IsNullOrWhiteSpace(input))
            return Array.Empty<string>();

        string[] words = Tokenize(input);
        if (words.Length == 0)
            return Array.Empty<string>();

        string firstWord = words[0];
        CommandDefinition[] matchingDefinitions = Definitions
            .Where(definition => definition.Forms.Any(form => form.Tokens[0].CanStartWith(firstWord)))
            .ToArray();
        if (matchingDefinitions.Length == 0)
            return Array.Empty<string>();

        handlesInput = true;
        bool hasTrailingWhitespace = char.IsWhiteSpace(input[^1]);
        var suggestions = new List<string>();

        if (!hasTrailingWhitespace)
        {
            string[] completedWords = words[..^1];
            string partialWord = words[^1];
            int partialWordStart = input.LastIndexOf(partialWord, StringComparison.Ordinal);
            string prefix = input[..partialWordStart];

            foreach (CommandForm form in matchingDefinitions.SelectMany(definition => definition.Forms))
            {
                foreach (CommandToken expected in GetFrontier(form, completedWords))
                {
                    foreach (string value in expected.GetSuggestions(partialWord))
                        AddSuggestion(suggestions, prefix + value, input);
                }
            }
        }

        if (hasTrailingWhitespace || matchingDefinitions.Any(definition => definition.Forms.Any(form => MatchesPrefix(form, words, 0, 0))))
        {
            foreach (CommandForm form in matchingDefinitions.SelectMany(definition => definition.Forms))
            {
                foreach (CommandToken expected in GetFrontier(form, words))
                {
                    foreach (string value in expected.GetSuggestions(string.Empty))
                    {
                        string suggestion = input.TrimEnd() + " " + value;
                        AddSuggestion(suggestions, suggestion, input);
                    }
                }
            }
        }

        return suggestions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static bool TryResolveHotkeyKey(string token, out Key key)
    {
        key = Key.None;
        if (token.Length == 1)
        {
            short virtualKey = VkKeyScan(token[0]);
            key = KeyInterop.KeyFromVirtualKey((byte)(virtualKey & 0xFF));
            return true;
        }

        string normalized = token.Trim().ToLowerInvariant();
        string tryName = normalized switch
        {
            "space" => "Space",
            "enter" or "return" => "Enter",
            "esc" or "escape" => "Escape",
            "pgup" or "pageup" => "PageUp",
            "pgdn" or "pagedown" => "PageDown",
            "num" => "NumPad",
            "left" => "Left",
            "right" => "Right",
            "up" => "Up",
            "down" => "Down",
            "tab" => "Tab",
            "backspace" or "bksp" => "Back",
            "del" or "delete" => "Delete",
            "home" => "Home",
            "end" => "End",
            _ => token
        };

        if (Regex.IsMatch(normalized, @"^f\d{1,2}$"))
            tryName = normalized.ToUpperInvariant();
        else if (Regex.IsMatch(normalized, @"^num\d$"))
            tryName = "NumPad" + normalized[^1];

        return Enum.TryParse(tryName, ignoreCase: true, out key);
    }

    private static string? GetHotkeyErrorMessage(string[] tokens, bool isIncomplete)
    {
        if (tokens.Length == 1)
            return Definitions.First(definition => definition.Id == "hotkey").Usage;

        string[] modifiers = { "ctrl", "control", "alt", "shift" };
        string[] keyTokens = tokens.Skip(1)
            .Where(token => !modifiers.Contains(token, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (keyTokens.Length != 1)
            return "Command failed: Expected exactly one key token after any modifiers (ctrl alt shift)";

        string keyToken = keyTokens[0];
        return $"Failed to parse hotkey name: '{keyToken}'. Try names like 'left', 'space', 'f1' or a single character.";
    }

    private static bool IsValidHotkeyKey(string token)
    {
        if (token.Length == 1)
            return true;

        if (token is "ctrl" or "control" or "alt" or "shift")
            return false;

        return TryResolveHotkeyKey(token, out _);
    }

    private static bool IsPositiveInteger(string token) =>
        int.TryParse(token, out int count) && count > 0;

    private static bool IsValidCustomCommandIndex(string token) =>
        token.StartsWith("c", StringComparison.OrdinalIgnoreCase) &&
        int.TryParse(token.AsSpan(1), out int index) && index is >= 1 and <= 24;

    private static bool IsNonnegativeInteger(string token) =>
        token.Length > 0 && token.All(char.IsDigit) && int.TryParse(token, out int index) && index >= 0;

    private static bool IsPositiveOffset(string token) =>
        token.StartsWith('+') && int.TryParse(token, out _);

    private static bool IsNegativeOffset(string token) =>
        token.StartsWith('-') && int.TryParse(token, out _);

    private static bool IsPositiveSeconds(string token) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) &&
        double.IsFinite(seconds) && seconds > 0;

    private static bool IsValidReverseSearchService(string token) =>
        new[] { "google", "g", "bing", "b", "yandex", "y", "tineye", "t", "saucenao", "s" }
            .Contains(token, StringComparer.OrdinalIgnoreCase);

    private static string GetReverseImageSearchErrorMessage(string[] tokens) => tokens.Length == 2
        ? "Invalid argument specifying reverse image search service."
        : "Usage: ris [service] (google, bing, yandex, tineye, or saucenao)";

    private static bool IsInteger(string token) => int.TryParse(token, out _);

    private static bool IsDimensionValue(string token) => token == "_" || int.TryParse(token, out _);

    internal static string GetDimensionUsageMessage() =>
        "Unexpected format. Usage: dim [int|+/-int|_] [int|+/-int|_]. Unsigned values set a dimension; signed values change it.";

    private static string GetFilmstripErrorMessage(string[] tokens)
    {
        if (tokens.Length > 1 && FilmstripPreview.TryMatch(tokens[1], out _))
            return "A workspace name is required for a filmstrip preview.";
        if (tokens.Length > 1 && TagQueryRoot.TryMatch(tokens[1], out _))
            return "A tag query is required for a filmstrip tag command.";
        if (tokens.Length > 1)
            return "Unknown filmstrip source: " + tokens[1];
        return "Usage: filmstrip [directory|recent|bookmarks|preview [workspace]]";
    }

    private static string GetOpenPathErrorMessage(string[] tokens)
    {
        if (tokens.Length > 1 && TagQueryRoot.TryMatch(tokens[1], out _))
            return $"Usage: {tokens[0]} {tokens[1]} [query]";
        return "Usage: open|o [path] or open!|o! [path]";
    }

    private static string GetWorkspaceErrorMessage(string[] tokens)
    {
        if (tokens.Length > 1 && WorkspacePreview.TryMatch(tokens[1], out _))
            return "A workspace name is required for a workspace preview.";
        return "Usage: ws save|save!|load|merge|preview|rename|delete|origin|list ...";
    }

    private static string GetCustomCommandErrorMessage(string[] tokens) =>
        $"Usage: {tokens[0]} set [command], {tokens[0]} view, or {tokens[0]} run";

    private static bool IsOpenTagQueryRoot(string[] tokens) =>
        tokens.Length > 1 &&
        (OpenPathRoot.TryMatch(tokens[0], out _) || OpenAllPathRoot.TryMatch(tokens[0], out _)) &&
        TagQueryRoot.TryMatch(tokens[1], out _);

    private static bool IsValidVolume(string token) =>
        double.TryParse(token, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out double value) &&
        double.IsFinite(value) && value is >= 0 and <= 100;

    private static bool IsValidPlaybackSpeed(string token) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
        double.IsFinite(value) && value > 0;

    private static bool IsValidVideoDelay(string token) => MainWindow.TryParseVideoDelay(token, out _);

    private static bool IsValidAbsoluteSeekTime(string token) =>
        !token.StartsWith('+') && !token.StartsWith('-') && TryParseSeekTime(token);

    private static bool IsValidForwardSeekTime(string token) =>
        token.Length > 1 && token[0] == '+' && TryParseSeekTime(token[1..]);

    private static bool IsValidBackwardSeekTime(string token) =>
        token.Length > 1 && token[0] == '-' && TryParseSeekTime(token[1..]);

    private static bool TryParseSeekTime(string token)
    {
        try
        {
            return MainWindow.TryParseTimeString(token).HasValue;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static string GetVideoTransportErrorMessage(string[] tokens)
    {
        string root = tokens[0].ToLowerInvariant();
        if (root == "seek")
        {
            if (tokens.Length == 1)
                return "Usage: seek previous, seek ?, or seek [time] (for example, seek 1:30)";
            string timeExpression = string.Join(' ', tokens.Skip(1));
            if (tokens[1].StartsWith('+') || tokens[1].StartsWith('-'))
                return $"Could not parse relative time '{timeExpression}'. Use formats like: '+90' (seconds), '+1:30' (m:s), '-30s'";
            return $"Could not parse time '{timeExpression}'. Use formats like: '90' (seconds), '1:30' (m:s), '1:30:45' (h:m:s), or '1h30m45s'";
        }

        if (root == "speed" && tokens.Length > 1)
            return "Invalid playback speed. Provide a positive multiplier, e.g. 'speed 2.5'.";
        if (root == "volume" && tokens.Length > 1)
            return double.TryParse(string.Join(' ', tokens.Skip(1)), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out _)
                ? "Volume must be from 0 to 100."
                : "Invalid volume value. Provide a number between 0 and 100.";
        if (root is "audio" or "subtitle" && tokens.Length >= 3)
        {
            string trackType = root;
            return $"Invalid {trackType}/video sync offset. Provide a time in seconds or milliseconds, e.g. {trackType} sync -0.25 or {trackType} sync 120ms.";
        }

        return root switch
        {
            "audio" => "Usage: audio sync [offset]; use 'audio sync' to show the current offset",
            "subtitle" => "Usage: subtitle sync [offset]; use 'subtitle sync' to show the current offset",
            "seek" => "Usage: seek previous, seek ?, or seek [time] (for example, seek 1:30)",
            _ => "Invalid or incomplete video command."
        };
    }

    private static string GetNudgeErrorMessage(string[] tokens)
    {
        bool hasValidDirection = NudgeDirection.TryMatch(tokens.ElementAtOrDefault(1) ?? string.Empty, out _);
        bool hasNumericX = IsInteger(tokens.ElementAtOrDefault(1) ?? string.Empty);
        if (tokens.Length == 2 && !hasValidDirection)
            return "Invalid nudge direction. Use left/right/up/down or numeric x y.";
        if (tokens.Length == 3 && !IsInteger(tokens[2]))
            return "Invalid numeric value for nudge.";
        if (tokens.Length == 3 && !hasNumericX && !hasValidDirection)
            return "Invalid nudge direction. Use left/right/up/down or numeric x y.";
        return "Usage: nudge left|right|up|down [count]  OR  nudge <x> <y>";
    }

    private static string GetVideoPositionErrorMessage(string[] tokens)
    {
        int triggerIndex = Array.FindIndex(tokens, token => token.Equals("trigger", StringComparison.OrdinalIgnoreCase));
        if (triggerIndex >= 0 && tokens.Length == triggerIndex + 2 && !IsPositiveInteger(tokens[^1]))
            return "Trigger count must be a positive integer";

        return tokens[0].ToLowerInvariant() switch
        {
            "set" => "Usage: set start, set end, set flag, or set trigger [count]",
            "clear" => "Usage: clear start, clear end, clear flag, or clear trigger",
            _ => "Usage: goto start, goto end, or goto flag"
        };
    }

    private static bool IsPageTarget(string token) =>
        new[] { "t", "this", "p", "prev", "previous", "n", "next", "pa", "na", "pi", "ni" }
            .Contains(token, StringComparer.OrdinalIgnoreCase) ||
        int.TryParse(token, out int pageIndex) && pageIndex is >= 1 and <= 20;

    private static CommandDefinition[] CreateDefinitions()
    {
        var hotkeyForms = new[]
        {
            new CommandForm(
                "simulate-hotkey",
                "Simulate a hotkey press. Modifiers are optional; exactly one key is required.",
                HotkeyRoot, HotkeyModifier, HotkeyKey)
        };

        var generalActionForms = new[]
        {
            new CommandForm("close-window", "Close this window.", CloseRoot),
            new CommandForm("close-all", "Close all windows.", CloseRoot, AllAction),
            new CommandForm("close-others", "Close all other windows.", CloseRoot, OthersAction),
            new CommandForm("close-selected", "Close all selected windows.", CloseRoot, SelectedAction),
            new CommandForm("close-empty", "Close all empty windows.", CloseRoot, EmptyAction),
            new CommandForm("close-origin", "Close windows from this window's workspace origin.", CloseOriginRoot, OriginAction),
            new CommandForm("selection-count", "Show the number of selected windows.", SelectionCountRoot),
            new CommandForm("selected-wrapper", "Execute the command on every selected window.", SelectedWrapperRoot, WrappedCommand),
            new CommandForm("select-window", "Select this window.", SelectRoot),
            new CommandForm("deselect-window", "Deselect this window.", DeselectRoot),
            new CommandForm("prevent-overlap", "Prevent this window from overlapping opted-in windows.", PreventOverlapRoot, OverlapWord),
            new CommandForm("prevent-overlap", "Prevent this window from overlapping opted-in windows.", PreventOverlapAliasRoot),
            new CommandForm("allow-overlap", "Allow this window to overlap other windows.", AllowOverlapRoot, OverlapWord),
            new CommandForm("allow-overlap", "Allow this window to overlap other windows.", AllowOverlapAliasRoot),
            new CommandForm("minimize-all", "Minimize all windows.", MinimizeRoot, AllAction),
            new CommandForm("minimize-others", "Minimize all other windows.", MinimizeRoot, OthersAction),
            new CommandForm("minimize-origin", "Minimize windows from this window's workspace origin.", MinimizeRoot, OriginAction),
            new CommandForm("unminimize-all", "Unminimize all windows.", UnminimizeRoot, AllAction),
            new CommandForm("unminimize-origin", "Unminimize windows from this window's workspace origin.", UnminimizeRoot, OriginAction),
            new CommandForm("shutdown", "Close all Cloudless instances and shut down the background process.", ShutdownRoot),
            new CommandForm("deflash", "Minimize and unminimize all windows.", DeflashRoot),
            new CommandForm("stack", "Resize and diagonally stack windows on the current page.", StackRoot),
            new CommandForm("flatten", "Send all windows to page 1 and change view to page 1.", FlattenRoot),
            new CommandForm("copy-image-path", "Copy the current image path to the clipboard.", CopyImagePathRoot),
            new CommandForm("reveal-image", "Reveal the current image in File Explorer.", RevealImageRoot),
            new CommandForm("help", "Open the command palette reference window.", HelpRoot)
        };

        var videoPositionForms = new[]
        {
            new CommandForm("clear-selection", "Clear the selection from all windows.", ClearSelectionRoot, SelectedAction),
            new CommandForm("clear-selection", "Clear the selection from all windows.", ClearSelectionAliasRoot),
            new CommandForm("invert-selection", "Invert the selection across all windows.", InvertSelectionRoot, SelectedAction),
            new CommandForm("invert-selection", "Invert the selection across all windows.", InvertSelectionAliasRoot),
            new CommandForm("video-set-start", "Set the custom video loop start to the current position.", PositionSetRoot, PositionStart),
            new CommandForm("video-set-end", "Set the custom video loop end to the current position.", PositionSetRoot, PositionEnd),
            new CommandForm("video-set-flag", "Set a custom video flag to the current position.", PositionSetRoot, PositionFlag),
            new CommandForm("video-clear-start", "Clear the custom video loop start.", PositionClearRoot, PositionStart),
            new CommandForm("video-clear-end", "Clear the custom video loop end.", PositionClearRoot, PositionEnd),
            new CommandForm("video-clear-flag", "Clear the custom video flag.", PositionClearRoot, PositionFlag),
            new CommandForm("video-goto-start", "Seek to the video start or custom loop start.", PositionGotoRoot, PositionStart),
            new CommandForm("video-goto-end", "Seek to the video end or custom loop end.", PositionGotoRoot, PositionEnd),
            new CommandForm("video-goto-flag", "Seek to the custom video flag.", PositionGotoRoot, PositionFlag),
            new CommandForm("video-set-trigger", "Mark this video as a slideshow trigger.", PositionSetRoot, TriggerRoot),
            new CommandForm("video-set-trigger", "Mark this video as a slideshow trigger after the specified number of hits.", PositionSetRoot, TriggerRoot, TriggerCount),
            new CommandForm("video-clear-trigger", "Clear the slideshow trigger for this video.", PositionClearRoot, TriggerRoot),
        };

        var sortForms = new[]
        {
            new CommandForm("sort", "Sort images by filename in ascending order.", SortRoot, SortName, SortAscending),
            new CommandForm("sort", "Sort images by filename in descending order.", SortRoot, SortName, SortDescending),
            new CommandForm("sort", "Sort images by modification date in ascending order.", SortRoot, SortDate, SortAscending),
            new CommandForm("sort", "Sort images by modification date in descending order.", SortRoot, SortDate, SortDescending)
        };

        var displayModeForms = new[]
        {
            new CommandForm("display-mode", "Stretch the image to fit the window.", DisplayModeRoot, DisplayStretch),
            new CommandForm("display-mode", "Zoom the image to fill the window.", DisplayModeRoot, DisplayZoom),
            new CommandForm("display-mode", "Fit the image within the window without distortion.", DisplayModeRoot, DisplayBest),
            new CommandForm("display-mode", "Fit the image without enlarging it.", DisplayModeRoot, DisplayBestNoZoom)
        };

        var reverseImageSearchForms = new[]
        {
            new CommandForm("reverse-image-search", "Search Google using the current image.", ReverseSearchRoot),
            new CommandForm("reverse-image-search", "Search the selected reverse-image-search service using the current image.", ReverseSearchRoot, ReverseSearchService)
        };

        var nudgeForms = new[]
        {
            new CommandForm("nudge-direction", "Move the window one pixel in the specified direction.", NudgeRoot, NudgeDirection),
            new CommandForm("nudge-direction", "Move the window by the specified number of pixels in the given direction.", NudgeRoot, NudgeDirection, NudgeCount),
            new CommandForm("nudge-vector", "Move the window by the specified horizontal and vertical pixel offsets.", NudgeRoot, NudgeX, NudgeY)
        };

        var videoTransportForms = new[]
        {
            new CommandForm("video-mute", "Mute the window's audio.", MuteRoot),
            new CommandForm("video-unmute", "Unmute the window's audio.", UnmuteRoot),
            new CommandForm("video-volume-query", "Show the current volume level.", VolumeRoot),
            new CommandForm("video-volume-set", "Set the volume from 0 to 100.", VolumeRoot, VolumeValue),
            new CommandForm("video-speed-query", "Show the current playback speed.", SpeedRoot),
            new CommandForm("video-speed-set", "Set the playback speed multiplier.", SpeedRoot, SpeedValue),
            new CommandForm("video-audio-sync-query", "Show the audio/video timing offset.", AudioRoot, AudioSync),
            new CommandForm("video-audio-sync-set", "Set the audio/video timing offset.", AudioRoot, AudioSync, AudioOffset),
            new CommandForm("video-subtitle-sync-query", "Show the subtitle/video timing offset.", SubtitleRoot, AudioSync),
            new CommandForm("video-subtitle-sync-set", "Set the subtitle/video timing offset.", SubtitleRoot, AudioSync, AudioOffset),
            new CommandForm("video-play", "Resume video playback.", PlayRoot),
            new CommandForm("video-pause", "Pause video playback.", PauseRoot),
            new CommandForm("video-sync", "Register this video in the page's synchronization group.", SyncRoot),
            new CommandForm("video-unsync", "Remove this video from the page's synchronization group.", UnsyncRoot),
            new CommandForm("video-seek-status", "Show the current video position and duration.", SeekRoot, SeekStatus),
            new CommandForm("video-seek-previous", "Seek to the video's previously saved closed position.", SeekRoot, SeekPrevious),
            new CommandForm("video-seek-absolute", "Seek to an absolute video time.", SeekRoot, SeekAbsoluteTime),
            new CommandForm("video-seek-forward", "Seek forward by a relative time.", SeekRoot, SeekForwardTime),
            new CommandForm("video-seek-backward", "Seek backward by a relative time.", SeekRoot, SeekBackwardTime)
        };

        var filmstripForms = new[]
        {
            new CommandForm("filmstrip-current-directory", "Open the filmstrip using its retained or current directory contents.", FilmstripRoot),
            new CommandForm("filmstrip-directory", "Open the filmstrip with the current directory contents.", FilmstripRoot, FilmstripDirectory),
            new CommandForm("filmstrip-directory-path", "Open the filmstrip with media from the specified file's directory or directory path.", FilmstripRoot, FilmstripDirectory, FilmstripDirectoryPath),
            new CommandForm("filmstrip-recent", "Open the filmstrip with recently viewed media.", FilmstripRoot, FilmstripRecent),
            new CommandForm("filmstrip-bookmarks", "Open the filmstrip with bookmarked media.", FilmstripRoot, FilmstripBookmarks),
            new CommandForm("filmstrip-preview", "Open the filmstrip with media from the named workspace.", FilmstripRoot, FilmstripPreview, FilmstripWorkspace),
            new CommandForm("filmstrip-current-directory", "Open the filmstrip using its retained or current directory contents.", FilmstripRoot, FilmstripCurrentAlias),
            new CommandForm("filmstrip-tag-query", "Open the filmstrip with files matching the tag query.", FilmstripRoot, TagQueryRoot, TagQuery)
        };

        var dimensionForms = new[]
        {
            new CommandForm("window-dimensions-query", "Show the current window size.", DimensionsRoot),
            new CommandForm("window-dimensions-set", "Set dimensions or apply signed size offsets; use _ to preserve one dimension.", DimensionsRoot, DimensionWidth, DimensionHeight)
        };

        var workspaceForms = new[]
        {
            new CommandForm("workspace-save", "Save the current workspace under the specified name.", WorkspaceRoot, WorkspaceSave, WorkspaceName),
            new CommandForm("workspace-save-force", "Overwrite the named workspace with the current workspace.", WorkspaceRoot, WorkspaceSaveForce, WorkspaceName),
            new CommandForm("workspace-load", "Load the named workspace and close currently open Cloudless windows.", WorkspaceRoot, WorkspaceLoad, WorkspaceName),
            new CommandForm("workspace-merge", "Load the named workspace without closing currently open windows.", WorkspaceRoot, WorkspaceMerge, WorkspaceName),
            new CommandForm("workspace-delete", "Delete the named workspace.", WorkspaceRoot, WorkspaceDelete, WorkspaceName),
            new CommandForm("workspace-rename", "Rename a workspace.", WorkspaceRoot, WorkspaceRename, WorkspaceOldName, WorkspaceNewName),
            new CommandForm("workspace-preview", "Open a gallery preview of the named workspace.", WorkspaceRoot, WorkspacePreview, WorkspaceName),
            new CommandForm("workspace-origin", "Show the workspace from which this window's current image originated.", WorkspaceRoot, WorkspaceOrigin),
            new CommandForm("workspace-origin-save", "Overwrite this window's origin workspace with the current workspace.", WorkspaceRoot, WorkspaceOrigin, WorkspaceOriginSave),
            new CommandForm("workspace-origin-load", "Load this window's origin workspace.", WorkspaceRoot, WorkspaceOrigin, WorkspaceOriginLoad),
            new CommandForm("workspace-list-current", "List saved workspaces containing the currently displayed media.", WorkspaceRoot, WorkspaceList),
            new CommandForm("workspace-list-path", "List saved workspaces containing the specified media path.", WorkspaceRoot, WorkspaceList, WorkspacePath),
            new CommandForm("workspace-reveal", "Reveal the workspace files directory in File Explorer.", WorkspaceRoot, WorkspaceRev),
            new CommandForm("workspace-undoload", "Load the workspace state saved before the most recent workspace load.", WorkspaceRoot, WorkspaceUndoload),
            new CommandForm("workspace-undocloseall", "Restore the workspace saved before the most recent close-all or shutdown.", WorkspaceRoot, WorkspaceUndocloseall),
            new CommandForm("workspace-quicksave", "Save the current workspace to the quicksave slot.", QuickSaveRoot),
            new CommandForm("workspace-quicksave-close", "Quicksave, then close all windows if saving succeeds.", QuickSaveRoot, QuickSaveClose),
            new CommandForm("workspace-quickload", "Load the quicksave workspace.", QuickLoadRoot),
            new CommandForm("workspace-quickmerge", "Merge the quicksave workspace without closing other windows.", QuickMergeRoot)
        };

        var pageForms = new List<CommandForm>
        {
            new("page-status", "Display the current page and all non-empty pages.", PageRoot, PageStatus),
            new("page-arrange", "Pack active pages into consecutive page indices.", PageRoot, PageArrange),
            new("page-view", "Change view to page [target].", PageRoot, PageTarget),
            new("page-send-window", "Send the current window to page [target].", PageRoot, PageTarget, SendAction),
            new("page-bring-window", "Send the current window to page [target], and change view to that page.", PageRoot, PageTarget, BringAction),
            new("page-send-page", "Send all windows on the current page to page [target].", PageRoot, PageTarget, SendAction, PageQualifier),
            new("page-bring-page", "Send all windows on the current page to page [target], and change view to that page.", PageRoot, PageTarget, BringAction, PageQualifier),
            new("page-clear", "Close all windows on page [target].", PageRoot, PageTarget, ClearAction),
            new("page-swap", "Swap all windows between page [target] and [secondTarget].", PageRoot, PageTarget, SwapAction, PageSecondTarget)
        };

        var slideshowForms = new List<CommandForm>();
        AddSlideshowForms(slideshowForms, SlideshowLongRoot, shortRoot: false);
        AddSlideshowForms(slideshowForms, SlideshowShortRoot, shortRoot: true);

        var tagForms = new[]
        {
            new CommandForm("tag-add", "Add one or more space-separated tags to the current media.", TagRoot, TagAdd, TagNames),
            new CommandForm("tag-remove", "Remove one or more space-separated tags from the current media.", TagRoot, TagRemove, TagNames),
            new CommandForm("tag-destroy", "Delete one or more tags entirely.", TagRoot, TagDestroy, TagNames),
            new CommandForm("tag-list", "List tags associated with the current media.", TagRoot, TagList)
        };

        var imageNavigationForms = new[]
        {
            new CommandForm("image-first", "Open the first image in the current directory.", FirstRoot),
            new CommandForm("image-last", "Open the last image in the current directory.", LastRoot),
            new CommandForm("image-recent", "Open the most recently loaded image.", RecentRoot),
            new CommandForm("image-recent-count", "Open the specified number of recent images in new windows.", RecentRoot, RecentCount),
            new CommandForm("image-index", "Open the image with this 1-based index in the current directory.", AbsoluteImageIndex),
            new CommandForm("image-forward-offset", "Jump forward by this number of images.", ForwardImageOffset),
            new CommandForm("image-backward-offset", "Jump backward by this number of images.", BackwardImageOffset),
            new CommandForm("image-filename-search", "Open the next image whose filename contains the query.", FilenameSearchQuery)
        };

        var openPathForms = new[]
        {
            new CommandForm("open-tag-query", "Open files matching the tag query (maximum 10).", OpenPathRoot, TagQueryRoot, TagQuery),
            new CommandForm("open-all-tag-query", "Open all files matching the tag query.", OpenAllPathRoot, TagQueryRoot, TagQuery),
            new CommandForm("open-path", "Open the image at a relative or absolute path, or open images in a directory (maximum 10).", OpenPathRoot, OpenPath),
            new CommandForm("open-all-path", "Open the image at a relative or absolute path, or open all images in a directory.", OpenAllPathRoot, OpenPath)
        };

        var galleryForms = new[]
        {
            new CommandForm("gallery-tag-query", "Open a gallery of files matching the tag query.", GalleryRoot, TagQueryRoot, TagQuery),
            new CommandForm("gallery-directory", "Open a gallery of media from the current directory.", GalleryRoot, GalleryDirectory),
            new CommandForm("gallery-directory-path", "Open a gallery of supported media from the specified file's directory or directory path.", GalleryRoot, GalleryDirectory, GalleryDirectoryPath)
        };

        var macroForms = new[]
        {
            new CommandForm("macro-list", "List all saved macros.", MacroRoot, MacroList),
            new CommandForm("macro-run", "Run the named macro.", MacroRoot, MacroRun, MacroName),
            new CommandForm("macro-delete", "Delete the named macro.", MacroRoot, MacroDelete, MacroName),
            new CommandForm("macro-record", "Record a command under the specified macro name.", MacroRoot, MacroRecord, MacroRecordName, MacroCommand)
        };

        var customCommandForms = new[]
        {
            new CommandForm("custom-command-set", "Save a command at the specified custom-command index.", CustomCommandRoot, CustomCommandSet, CustomCommandText),
            new CommandForm("custom-command-view", "View the command saved at the specified index.", CustomCommandRoot, CustomCommandView),
            new CommandForm("custom-command-run", "Run the command saved at the specified index.", CustomCommandRoot, CustomCommandRun)
        };

        var commandWrapperForms = new[]
        {
            new CommandForm("command-wrapper-all", "Execute the command on each other window and then this window.", AllWrapperRoot, WrappedCommand),
            new CommandForm("command-wrapper-others", "Execute the command on each other window.", OthersWrapperRoot, WrappedCommand)
        };

        return new[]
        {
            new CommandDefinition(
                "hotkey",
                "Usage: hotkey [modifiers] [key] (for example, hotkey ctrl shift c)",
                hotkeyForms),
            new CommandDefinition(
                "general-actions",
                "Improper usage, consult command palette reference",
                generalActionForms),
            new CommandDefinition(
                "page",
                "Usage: p [target], p arrange, or p [target] send|bring|clear|swap ...",
                pageForms),
            new CommandDefinition(
                "slideshow",
                "Usage: slideshow [seconds] [shuffle], slideshow triggers [shuffle], slideshow pause (ss p), slideshow resume (ss r), slideshow status, slideshow stop (ss s), or slideshow next",
                slideshowForms),
            new CommandDefinition(
                "tags",
                "Usage: tag add [tags], tag remove [tags], tag destroy [tag], or tag list",
                tagForms),
            new CommandDefinition(
                "image-navigation",
                "Usage: [Int], +[Int], -[Int], r [Int], first, last, or /[query]",
                imageNavigationForms),
            new CommandDefinition(
                "open-path",
                "Usage: open|o [path] or open!|o! [path]",
                openPathForms),
            new CommandDefinition(
                "video-position",
                "Usage: set/clear/goto video position commands",
                videoPositionForms),
            new CommandDefinition(
                "sort",
                "Usage: sort name|date asc|desc",
                sortForms),
            new CommandDefinition(
                "display-mode",
                "Usage: dm stretch|zoom|best|bestnozoom (or dm 1-4)",
                displayModeForms),
            new CommandDefinition(
                "reverse-image-search",
                "Usage: ris [service] (google, bing, yandex, tineye, or saucenao)",
                reverseImageSearchForms),
            new CommandDefinition(
                "window-nudge",
                "Usage: nudge left|right|up|down [count]  OR  nudge <x> <y>",
                nudgeForms),
            new CommandDefinition(
                "video-transport",
                "Video playback and timing commands",
                videoTransportForms),
            new CommandDefinition(
                "filmstrip",
                "Usage: filmstrip [directory|recent|bookmarks|preview [workspace]]",
                filmstripForms),
            new CommandDefinition(
                "gallery",
                "Usage: gallery tag|t [query] or gallery directory|d [path]",
                galleryForms),
            new CommandDefinition(
                "window-dimensions",
                GetDimensionUsageMessage(),
                dimensionForms),
            new CommandDefinition(
                "workspace",
                "Usage: ws save|save!|load|merge|preview|rename|delete|origin|list ...",
                workspaceForms),
            new CommandDefinition(
                "macros",
                "Usage: macro list, macro run [name], macro delete [name], or macro record [name] [command]",
                macroForms),
            new CommandDefinition(
                "custom-commands",
                "Usage: c[Int 1-24] set [command], c[Int 1-24] view, or c[Int 1-24] run",
                customCommandForms),
            new CommandDefinition(
                "command-wrappers",
                "Usage: all [command], others [command], or sel [command]",
                commandWrapperForms)
        };
    }

    private static void AddSlideshowForms(List<CommandForm> forms, CommandToken root, bool shortRoot)
    {
        if (shortRoot)
        {
            forms.Add(new CommandForm("slideshow-start-triggers-shuffle", "Start a shuffled trigger-based slideshow.", root, SlideshowShuffle, SlideshowTriggers));
            forms.Add(new CommandForm("slideshow-start-triggers-shuffle", "Start a shuffled trigger-based slideshow.", root, SlideshowTriggers, SlideshowShuffle));
            forms.Add(new CommandForm("slideshow-start-triggers", "Start a trigger-based slideshow.", root, SlideshowTriggers));
        }
        else
        {
            forms.Add(new CommandForm("slideshow-stop", "Stop the current slideshow.", root, SlideshowStop));
        }

        forms.Add(new CommandForm("slideshow-next", "Advance to the next slide.", root, SlideshowNext));
        forms.Add(new CommandForm("slideshow-pause", "Pause the current slideshow.", root, shortRoot ? SlideshowPauseShort : SlideshowPause));
        forms.Add(new CommandForm("slideshow-resume", "Resume the paused slideshow.", root, shortRoot ? SlideshowResumeShort : SlideshowResume));
        forms.Add(new CommandForm("slideshow-status", "Show the current slideshow state.", root, SlideshowStatus));

        if (shortRoot)
            forms.Add(new CommandForm("slideshow-stop", "Stop the current slideshow.", root, SlideshowStopShort));
        else
        {
            forms.Add(new CommandForm("slideshow-start-triggers", "Start a trigger-based slideshow.", root, SlideshowTriggers));
            forms.Add(new CommandForm("slideshow-start-triggers-shuffle", "Start a shuffled trigger-based slideshow.", root, SlideshowTriggers, SlideshowShuffle));
            forms.Add(new CommandForm("slideshow-start-triggers-shuffle", "Start a shuffled trigger-based slideshow.", root, SlideshowShuffle, SlideshowTriggers));
        }

        forms.Add(new CommandForm("slideshow-start-interval", "Start an interval slideshow.", root, SlideshowInterval));
        forms.Add(new CommandForm("slideshow-start-interval-shuffle", "Start an interval slideshow with shuffled pages.", root, SlideshowInterval, SlideshowShuffle));
        forms.Add(new CommandForm("slideshow-start-interval-shuffle", "Start an interval slideshow with shuffled pages.", root, SlideshowShuffle, SlideshowInterval));
    }

    private static bool TryMatchComplete(CommandForm form, string[] words, out Dictionary<string, List<string>> arguments)
    {
        return TryMatchComplete(form.Tokens, words, 0, 0,
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase), out arguments);
    }

    private static bool TryMatchComplete(
        CommandToken[] parts,
        string[] words,
        int partIndex,
        int wordIndex,
        Dictionary<string, List<string>> values,
        out Dictionary<string, List<string>> arguments)
    {
        if (partIndex == parts.Length)
        {
            arguments = values;
            return wordIndex == words.Length;
        }

        CommandToken part = parts[partIndex];
        if (part.ConsumesRest)
        {
            if (wordIndex >= words.Length || !part.TryMatch(string.Join(' ', words.Skip(wordIndex)), out string restValue))
            {
                arguments = values;
                return false;
            }

            var restValues = Clone(values);
            AddValue(restValues, part.Name, restValue);
            return TryMatchComplete(parts, words, partIndex + 1, words.Length, restValues, out arguments);
        }

        if (part.IsRepeatable)
        {
            var matchedValues = new List<(int WordIndex, Dictionary<string, List<string>> Values)> { (wordIndex, Clone(values)) };
            int nextWord = wordIndex;
            var nextValues = Clone(values);
            while (nextWord < words.Length && part.TryMatch(words[nextWord], out string value))
            {
                AddValue(nextValues, part.Name, value);
                matchedValues.Add((++nextWord, Clone(nextValues)));
            }

            foreach (var state in matchedValues.OrderByDescending(state => state.WordIndex))
            {
                if (TryMatchComplete(parts, words, partIndex + 1, state.WordIndex, state.Values, out arguments))
                    return true;
            }

            arguments = values;
            return false;
        }

        if (wordIndex >= words.Length || !part.TryMatch(words[wordIndex], out string matchedValue))
        {
            arguments = values;
            return false;
        }

        var next = Clone(values);
        AddValue(next, part.Name, matchedValue);
        return TryMatchComplete(parts, words, partIndex + 1, wordIndex + 1, next, out arguments);
    }

    private static bool MatchesPrefix(CommandForm form, string[] words, int partIndex, int wordIndex)
    {
        if (wordIndex == words.Length)
            return true;
        if (partIndex == form.Tokens.Length)
            return false;

        CommandToken part = form.Tokens[partIndex];
        if (part.ConsumesRest)
        {
            string rest = string.Join(' ', words.Skip(wordIndex));
            return part.TryMatch(rest, out _) && MatchesPrefix(form, words, partIndex + 1, words.Length);
        }

        if (part.IsRepeatable)
        {
            if (MatchesPrefix(form, words, partIndex + 1, wordIndex))
                return true;
            return part.TryMatch(words[wordIndex], out _) && MatchesPrefix(form, words, partIndex, wordIndex + 1);
        }

        return part.TryMatch(words[wordIndex], out _) && MatchesPrefix(form, words, partIndex + 1, wordIndex + 1);
    }

    private static IReadOnlyList<CommandToken> GetFrontier(CommandForm form, string[] words)
    {
        var result = new List<CommandToken>();
        CollectFrontier(form, words, 0, 0, result);
        return result.Distinct().ToArray();
    }

    private static void CollectFrontier(CommandForm form, string[] words, int partIndex, int wordIndex, List<CommandToken> result)
    {
        if (partIndex == form.Tokens.Length)
            return;

        CommandToken part = form.Tokens[partIndex];
        if (wordIndex == words.Length)
        {
            result.Add(part);
            if (part.IsRepeatable)
                CollectFrontier(form, words, partIndex + 1, wordIndex, result);
            return;
        }

        if (part.ConsumesRest)
        {
            string rest = string.Join(' ', words.Skip(wordIndex));
            if (part.TryMatch(rest, out _))
                CollectFrontier(form, words, partIndex + 1, words.Length, result);
            return;
        }

        if (part.IsRepeatable)
        {
            CollectFrontier(form, words, partIndex + 1, wordIndex, result);
            if (part.TryMatch(words[wordIndex], out _))
                CollectFrontier(form, words, partIndex, wordIndex + 1, result);
        }
        else if (part.TryMatch(words[wordIndex], out _))
        {
            CollectFrontier(form, words, partIndex + 1, wordIndex + 1, result);
        }
    }

    private static string[] Tokenize(string input) =>
        input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static void AddValue(Dictionary<string, List<string>> values, string name, string value)
    {
        if (!values.TryGetValue(name, out List<string>? found))
        {
            found = new List<string>();
            values[name] = found;
        }
        found.Add(value);
    }

    private static Dictionary<string, List<string>> Clone(Dictionary<string, List<string>> values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.OrdinalIgnoreCase);

    private static void AddSuggestion(List<string> suggestions, string candidate, string input)
    {
        if (candidate.Length > input.Length && candidate.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            suggestions.Add(candidate);
    }

    private sealed class CommandDefinition
    {
        public string Id { get; }
        public string Usage { get; }
        public IReadOnlyList<CommandForm> Forms { get; }

        public CommandDefinition(string id, string usage, IEnumerable<CommandForm> forms)
        {
            Id = id;
            Usage = usage;
            Forms = forms.ToArray();
        }
    }

    private sealed class CommandForm
    {
        public string Operation { get; }
        public string Description { get; }
        public CommandToken[] Tokens { get; }

        public CommandForm(string operation, string description, params CommandToken[] tokens)
        {
            Operation = operation;
            Description = description;
            Tokens = tokens;
        }
    }

    private sealed class CommandToken
    {
        private readonly LiteralValue[] _literalValues;
        private readonly Func<string, bool>? _isValidArgument;
        private readonly LiteralValue[] _suggestedValues;

        public string Name { get; }
        public bool IsRepeatable { get; }
        public bool ConsumesRest { get; }
        public string DisplayText { get; }
        public IEnumerable<string> SuggestedValues => _suggestedValues.Select(value => value.Canonical);
        public IEnumerable<string> GetLiteralValues() => _literalValues.SelectMany(value => value.AllValues);

        private CommandToken(
            string name,
            string displayText,
            LiteralValue[] literalValues,
            Func<string, bool>? isValidArgument,
            LiteralValue[] suggestedValues,
            bool isRepeatable,
            bool consumesRest = false)
        {
            Name = name;
            DisplayText = displayText;
            _literalValues = literalValues;
            _isValidArgument = isValidArgument;
            _suggestedValues = suggestedValues;
            IsRepeatable = isRepeatable;
            ConsumesRest = consumesRest;
        }

        public static CommandToken Literal(string name, string canonical, params string[] aliases) =>
            new(name, string.Join('/', new[] { canonical }.Concat(aliases)), new[] { new LiteralValue(canonical, aliases) }, null, Array.Empty<LiteralValue>(), false);

        public static CommandToken LiteralWithSuggestionAlias(string name, string canonical, LiteralValue suggestion) =>
            new(name, string.Join('/', suggestion.AllValues), new[] { new LiteralValue(canonical) }, null, new[] { suggestion }, false);

        public static CommandToken LiteralWithHiddenAlias(string name, string canonical, string alias) =>
            new(name, canonical, new[] { new LiteralValue(canonical, alias) }, null, new[] { new LiteralValue(canonical) }, false);

        public static CommandToken Literal(string name, string displayText, LiteralValue first, LiteralValue second, LiteralValue third, bool repeatable = false) =>
            new(name, displayText, new[] { first, second, third }, null, Array.Empty<LiteralValue>(), repeatable);

        public static CommandToken LiteralOptions(string name, string displayText, params LiteralValue[] values) =>
            new(name, displayText, values, null, Array.Empty<LiteralValue>(), false);

        public static CommandToken Argument(string name, Func<string, bool> validator, params string[] suggestions) =>
            new(name, $"[{name}]", Array.Empty<LiteralValue>(), validator,
                suggestions.Select(value => new LiteralValue(value)).ToArray(), false);

        public static CommandToken ArgumentWithAliases(string name, Func<string, bool> validator, params LiteralValue[] suggestions) =>
            new(name, $"[{name}]", Array.Empty<LiteralValue>(), validator, suggestions, false);

        public static CommandToken Argument(string name, Func<string, bool> validator, string displayText) =>
            new(name, displayText, Array.Empty<LiteralValue>(), validator, Array.Empty<LiteralValue>(), false);

        public static CommandToken ArgumentRest(string name, Func<string, bool> validator, string? displayText = null) =>
            new(name, displayText ?? $"[{name}]", Array.Empty<LiteralValue>(), validator, Array.Empty<LiteralValue>(), false, true);

        public bool TryMatch(string word, out string canonicalValue)
        {
            foreach (LiteralValue literal in _literalValues)
            {
                if (literal.Matches(word))
                {
                    canonicalValue = literal.Canonical;
                    return true;
                }
            }

            if (_isValidArgument?.Invoke(word) == true)
            {
                canonicalValue = word;
                return true;
            }

            canonicalValue = string.Empty;
            return false;
        }

        public bool CanStartWith(string prefix) =>
            _literalValues.Any(value => value.AllValues.Any(candidate => candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        public IEnumerable<string> GetSuggestions(string prefix)
        {
            LiteralValue[] suggestionValues = _suggestedValues.Length > 0 ? _suggestedValues : _literalValues;
            foreach (LiteralValue value in suggestionValues)
            {
                string[] candidates = value.AllValues
                    .Where(candidate => candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (candidates.Length > 0)
                    yield return candidates.OrderByDescending(candidate => candidate.Length).First();
            }
        }
    }

    private sealed record LiteralValue(string Canonical, params string[] Aliases)
    {
        public IEnumerable<string> AllValues => new[] { Canonical }.Concat(Aliases);
        public bool Matches(string value) => AllValues.Contains(value, StringComparer.OrdinalIgnoreCase);
    }
}

internal enum CommandParseStatus
{
    Unrecognized,
    Complete,
    Incomplete,
    Invalid
}

internal sealed class CommandParseResult
{
    public static CommandParseResult Unrecognized { get; } = new(null, null,
        new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase), CommandParseStatus.Unrecognized);

    private readonly Dictionary<string, List<string>> _arguments;
    public bool IsRecognized => Status is CommandParseStatus.Complete or CommandParseStatus.Incomplete or CommandParseStatus.Invalid;
    public bool IsComplete => Status == CommandParseStatus.Complete;
    public CommandParseStatus Status { get; }
    public string? DefinitionId { get; }
    public string? Operation { get; }
    public string? ErrorMessage { get; }
    public IReadOnlyList<string> SequenceCommands { get; }
    public IReadOnlyList<CommandParseResult> Sequence { get; }

    internal CommandParseResult(
        string? definitionId,
        string? operation,
        Dictionary<string, List<string>> arguments,
        CommandParseStatus status,
        string? errorMessage = null,
        IReadOnlyList<string>? sequenceCommands = null,
        IReadOnlyList<CommandParseResult>? sequence = null)
    {
        _arguments = arguments;
        Status = status;
        Operation = operation;
        ErrorMessage = errorMessage;
        DefinitionId = definitionId;
        SequenceCommands = sequenceCommands ?? Array.Empty<string>();
        Sequence = sequence ?? Array.Empty<CommandParseResult>();
    }

    internal static CommandParseResult CreateSequence(string[] commands, CommandParseResult[] parsedCommands) =>
        new("command-chain", "command-chain",
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            CommandParseStatus.Complete,
            sequenceCommands: commands,
            sequence: parsedCommands);

    public string? GetArgument(string name) => _arguments.TryGetValue(name, out List<string>? values) ? values.FirstOrDefault() : null;
    public IReadOnlyList<string> GetArguments(string name) => _arguments.TryGetValue(name, out List<string>? values) ? values : Array.Empty<string>();
}
