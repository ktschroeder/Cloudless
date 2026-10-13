using System.Text.RegularExpressions;
using Cloudless;

namespace Cloudless.ReferenceData;

public static class CommandSuggestionProvider
{
    public static string? GetSuggestion(string input)
    {
        return GetSuggestions(input).FirstOrDefault();
    }

    public static IReadOnlyList<string> GetSuggestions(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Array.Empty<string>();

        IReadOnlyList<string> catalogSuggestions = CommandSyntaxCatalog.GetSuggestions(input, out bool catalogHandlesInput);
        if (catalogHandlesInput)
        {
            if (!input.Contains(' ') && "close".StartsWith(input, StringComparison.OrdinalIgnoreCase) &&
                catalogSuggestions.Any(candidate => candidate.StartsWith("clear", StringComparison.OrdinalIgnoreCase)))
            {
                return catalogSuggestions
                    .OrderBy(candidate => candidate.StartsWith("close", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ToArray();
            }

            return catalogSuggestions
                .OrderByDescending(candidate => candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Length)
                .ThenBy(candidate => candidate.Count(character => character == ' '))
                .ToArray();
        }

        return Array.Empty<string>();
    }
}
