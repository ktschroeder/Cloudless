using System.Text.RegularExpressions;
namespace Cloudless;

internal static class CommandSyntaxValidator
{
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

        command = command.Trim().ToLowerInvariant();
        if (command.Length == 0)
            return false;

        CommandParseResult catalogParse = CommandSyntaxCatalog.Parse(command);
        if (catalogParse.IsRecognized)
        {
            if (catalogParse.DefinitionId == "command-chain")
                return catalogParse.SequenceCommands.Count > 0 &&
                       catalogParse.SequenceCommands.All(part => IsValidCommand(part, nestingDepth + 1));
            if (catalogParse.DefinitionId == "command-wrappers")
                return catalogParse.IsComplete &&
                       IsValidCommand(catalogParse.GetArgument("wrappedCommand")!, nestingDepth + 1);
            return catalogParse.IsComplete;
        }

        if (Regex.IsMatch(command, @"^d$", RegexOptions.IgnoreCase))
            return false;

        if (Regex.IsMatch(command, @"^echo\s+.+$", RegexOptions.IgnoreCase))
            return true;

        return false;
    }

}
