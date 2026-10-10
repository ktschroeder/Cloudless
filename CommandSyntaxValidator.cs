using System.Globalization;
using System.Text.RegularExpressions;
using Cloudless.ReferenceData;

namespace Cloudless;

internal static class CommandSyntaxValidator
{
    private static readonly HashSet<string> IncompleteReferenceCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "b", "d", "e", "f", "l", "m", "p", "s", "s!", "um",
        "tag add", "tag remove", "tag destroy", "t add", "t a", "t remove", "t r", "t destroy"
    };

    internal static bool IsValidCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return IsValidCommand(input.Trim(), 0);
    }

    private static bool IsValidCommand(string command, int nestingDepth)
    {
        if (nestingDepth > 8)
            return false;

        string[] chainedCommands = command.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (chainedCommands.Length > 1)
            return chainedCommands.All(part => IsValidCommand(part, nestingDepth + 1));

        command = command.Trim().ToLowerInvariant();
        if (command.Length == 0)
            return false;

        if (command is "slideshow" or "ss")
            return false;

        if (command.Equals("tag", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("t", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("l", StringComparison.OrdinalIgnoreCase))
            return false;

        if (Regex.IsMatch(command, @"^(?:fs|filmstrip)\s+(?:preview|p|workspace|ws)$", RegexOptions.IgnoreCase))
            return false;

        if (Regex.IsMatch(command, @"^(?:open|open!|fs|filmstrip|o|o!|gallery)\s+(?:tag|t)$", RegexOptions.IgnoreCase) ||
            command is "open" or "open!")
            return false;

        if (Regex.IsMatch(command, @"^(?:hotkey|hk)\s+.+$", RegexOptions.IgnoreCase))
            return IsValidHotkeyCommand(command);

        if (Regex.IsMatch(command, @"^(?:set\s+(?:start|s|end|e|flag|f)|clear\s+(?:start|s|end|e|flag|f|trigger|ss trigger|slideshow trigger))$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(command, @"^set\s+(?:(?:ss|slideshow)\s+)?trigger(?:\s+[1-9]\d*)?$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(command, @"^clear\s+(?:(?:ss|slideshow)\s+)?trigger$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(command, @"^goto\s+(?:start|end|flag|f)$", RegexOptions.IgnoreCase))
            return true;

        if (Regex.IsMatch(command, @"^(?:c|close)\s+(?:all|others|empty)$", RegexOptions.IgnoreCase) ||
            command.Equals("c origin", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(command, @"^m\s+(?:all|others|origin)$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(command, @"^um\s+(?:all|origin)$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(command, @"^(?:shutdown|sd|deflash|df|first|last|flatten|qs|ql|qm|ws\s+rev)$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(command, @"^ws\s+origin(?:\s+(?:s!?|save!?|l|load))?$", RegexOptions.IgnoreCase))
            return true;

        if (Regex.IsMatch(command, @"^(?:tag|t)\s+(?:list|l)$", RegexOptions.IgnoreCase))
            return true;

        if (CommandSuggestionProvider.IsCompleteReferenceCommand(command) && !IncompleteReferenceCommands.Contains(command))
            return true;

        var customCommandMatch = Regex.Match(command, @"^c(?<index>\d{1,2})\s+(?<action>set|view|run)(?:\s+(?<argument>.+))?$", RegexOptions.IgnoreCase);
        if (customCommandMatch.Success && int.TryParse(customCommandMatch.Groups["index"].Value, out int customIndex) && customIndex is >= 1 and <= 24)
        {
            string action = customCommandMatch.Groups["action"].Value;
            return action.Equals("set", StringComparison.OrdinalIgnoreCase)
                ? customCommandMatch.Groups["argument"].Success
                : !customCommandMatch.Groups["argument"].Success;
        }

        var wrapperMatch = Regex.Match(command, @"^(?<wrapper>all|others)\s+(?<command>.+)$", RegexOptions.IgnoreCase);
        if (wrapperMatch.Success)
            return IsValidCommand(wrapperMatch.Groups["command"].Value, nestingDepth + 1);

        var macroMatch = Regex.Match(command, @"^macro\s+(?<action>list|run|delete|record)(?:\s+(?<arguments>.+))?$", RegexOptions.IgnoreCase);
        if (macroMatch.Success)
        {
            string action = macroMatch.Groups["action"].Value;
            bool hasArguments = macroMatch.Groups["arguments"].Success;
            return action.Equals("list", StringComparison.OrdinalIgnoreCase)
                ? !hasArguments
                : action.Equals("record", StringComparison.OrdinalIgnoreCase)
                    ? hasArguments && Regex.IsMatch(macroMatch.Groups["arguments"].Value, @"^\S+\s+.+$")
                    : hasArguments && !string.IsNullOrWhiteSpace(macroMatch.Groups["arguments"].Value);
        }

        if (Regex.IsMatch(command, @"^(?:nudge|n)\s+(?:left|right|up|down|l|r|u|d)$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(command, @"^(?:nudge|n)\s+(?:left|right|up|down|l|r|u|d)\s+-?\d+$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(command, @"^(?:nudge|n)\s+-?\d+\s+-?\d+$", RegexOptions.IgnoreCase))
            return true;

        if (Regex.IsMatch(command, @"^sort\s+(?:name|date)\s+(?:asc|desc)$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(command, @"^dm\s+(?:stretch|zoom|best|bestnozoom|[1-4])$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(command, @"^ris\s+(?:google|bing|yandex|tineye|saucenao|g|b|y|t|s)$", RegexOptions.IgnoreCase))
            return true;

        if (Regex.IsMatch(command, @"^ws\s+(?:save!?|s!?|load|l|merge|m|delete|rename|r)\s+.+$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(command, @"^ws\s+(?:preview|p)\s+.+$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(command, @"^ws\s+list(?:\s+.+)?$", RegexOptions.IgnoreCase))
            return true;

        if (Regex.IsMatch(command, @"^(?:filmstrip|fs)(?:\s+(?:directory|d|recent|r|bookmarks|b))?$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(command, @"^(?:filmstrip|fs)\s+(?:preview|p|workspace|ws)\s+.+$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(command, @"^d$", RegexOptions.IgnoreCase))
            return false;

        if (Regex.IsMatch(command, @"^(?:tag|t)\s+(?:add|a|remove|r)\s+.+$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(command, @"^(?:tag|t)\s+destroy\s+\S.*$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(command, @"^(?:fs|filmstrip|open|o|open!|o!|gallery)\s+(?:tag|t)\s+.+$", RegexOptions.IgnoreCase))
            return true;

        if (Regex.IsMatch(command, @"^(?:open|o|open!|o!)\s+.+$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(command, @"^r\s+[1-9]\d*$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(command, @"^\+\s*\d+$") || Regex.IsMatch(command, @"^-\s*\d+$") || Regex.IsMatch(command, @"^\d+$"))
            return true;
        if (Regex.IsMatch(command, @"^/\S.+$"))
            return true;

        if (Regex.IsMatch(command, @"^volume\s+\d+(?:\.\d+)?$", RegexOptions.IgnoreCase) &&
            double.TryParse(command.Split(' ').Last(), NumberStyles.Number, CultureInfo.InvariantCulture, out double volume))
            return volume is >= 0 and <= 100;
        if (Regex.IsMatch(command, @"^speed\s+\d+(?:\.\d+)?$", RegexOptions.IgnoreCase) &&
            double.TryParse(command.Split(' ').Last(), NumberStyles.Number, CultureInfo.InvariantCulture, out double speed))
            return speed > 0;
        if (Regex.IsMatch(command, @"^seek\s+(?:\?|previous|p|[+-]?(?:\d+(?:\.\d+)?|\d+:\d+(?::\d+)?|\d+h\d*m?\d*s?))$", RegexOptions.IgnoreCase))
            return true;

        if (Regex.IsMatch(command, @"^(?:audio|subtitle)\s+sync(?:\s+[-+]?\d+(?:\.\d+)?(?:ms|s)?)?$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(command, @"^set\s+trigger(?:\s+[1-9]\d*)?$", RegexOptions.IgnoreCase))
            return true;

        if (command.Equals("dim", StringComparison.OrdinalIgnoreCase))
            return true;
        if (Regex.IsMatch(command, @"^dim\s+(?:\d+|[+-]\d+|_)\s+(?:\d+|[+-]\d+|_)$", RegexOptions.IgnoreCase))
        {
            string[] dimensions = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
            return dimensions.Any(dimension => dimension != "_");
        }

        if (Regex.IsMatch(command, @"^echo\s+.+$", RegexOptions.IgnoreCase))
            return true;

        if (Regex.IsMatch(command, @"^p(?:age)?\s+\?$") || Regex.IsMatch(command, @"^(?:p|page)\s+arrange$", RegexOptions.IgnoreCase))
            return true;
        if (TryValidatePageCommand(command))
            return true;

        var slideshowMatch = Regex.Match(command, @"^(?:slideshow|ss)\s+(?<arguments>.+)$", RegexOptions.IgnoreCase);
        if (slideshowMatch.Success)
        {
            string arguments = slideshowMatch.Groups["arguments"].Value;
            if (arguments is "stop" or "next" or "n")
                return true;

            string[] parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool hasTriggers = parts.Any(part => part is "triggers" or "t");
            bool validParts = parts.All(part =>
                part is "shuffle" or "s" or "triggers" or "t" ||
                double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
            if (!validParts)
                return false;

            string? timeToken = parts.LastOrDefault(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
            if (timeToken != null)
                return double.TryParse(timeToken, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && seconds > 0;

            return hasTriggers;
        }

        return false;
    }

    private static bool IsValidHotkeyCommand(string command)
    {
        string[] tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
        string[] modifiers = { "ctrl", "control", "alt", "shift" };
        string[] keyTokens = tokens.Where(token => !modifiers.Contains(token, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (keyTokens.Length != 1)
            return false;

        string key = keyTokens[0];
        if (key.Length == 1)
            return true;

        string keyName = key.ToLowerInvariant() switch
        {
            "return" => "Enter",
            "esc" => "Escape",
            "pgup" => "PageUp",
            "pgdn" => "PageDown",
            "backspace" or "bksp" => "Back",
            "del" => "Delete",
            "num" => "NumPad",
            _ => key
        };

        if (Regex.IsMatch(key, @"^f\d{1,2}$", RegexOptions.IgnoreCase))
            keyName = key.ToUpperInvariant();
        else if (Regex.IsMatch(key, @"^num\d$", RegexOptions.IgnoreCase))
            keyName = "NumPad" + key[^1];

        return Enum.TryParse<System.Windows.Input.Key>(keyName, ignoreCase: true, out _);
    }

    private static bool TryValidatePageCommand(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !(parts[0].Equals("p", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("page", StringComparison.OrdinalIgnoreCase)))
            return false;

        if (!IsPageTarget(parts[1]))
            return false;
        if (parts.Length == 2)
            return true;

        string action = parts[2];
        if (action is not ("send" or "s" or "bring" or "b" or "clear" or "c" or "swap" or "x"))
            return false;
        if (parts.Length == 3)
            return action is "send" or "s" or "bring" or "b" or "clear" or "c";
        if (parts.Length == 4)
            return (action is "send" or "s" or "bring" or "b") && (parts[3] is "page" or "p") ||
                   (action is "swap" or "x") && IsPageTarget(parts[3]);
        return false;
    }

    private static bool IsPageTarget(string token) =>
        new[] { "t", "p", "n", "pa", "na", "pi", "ni", "this", "prev", "previous", "next" }
            .Contains(token, StringComparer.OrdinalIgnoreCase) ||
        int.TryParse(token, out int target) && target is >= 1 and <= 20;
}
