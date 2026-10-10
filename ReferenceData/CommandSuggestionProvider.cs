using System.Text.RegularExpressions;
using System.Text.RegularExpressions;

namespace Cloudless.ReferenceData;

public static class CommandSuggestionProvider
{
    private static readonly string[] AdditionalCommands =
    {
        "close", "close all", "close others", "close empty"
    };
    private static readonly string[] CommandContinuations =
    {
        "filmstrip directory", "filmstrip recent", "filmstrip bookmarks", "filmstrip preview",
        "nudge left", "nudge right", "nudge up", "nudge down",
        "sort name asc", "sort name desc", "sort date asc", "sort date desc",
        "dm stretch", "dm zoom", "dm best", "dm bestnozoom", "dm 1", "dm 2", "dm 3", "dm 4",
        "ris google", "ris bing", "ris yandex", "ris tineye", "ris saucenao",
        "tag add", "tag remove", "tag destroy", "tag list",
        "slideshow stop", "slideshow next", "slideshow triggers",
        "slideshow shuffle triggers", "slideshow triggers shuffle"
    };
    private static readonly (string[] Chained, string[] Complete, string[] Parameterized) Candidates = BuildCandidates();

    public static string? GetSuggestion(string input)
    {
        return GetSuggestions(input).FirstOrDefault();
    }

    internal static bool IsCompleteReferenceCommand(string input) =>
        Candidates.Complete.Contains(input, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GetSuggestions(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Array.Empty<string>();

        return Candidates.Complete
            .Concat(Candidates.Chained)
            .Concat(Candidates.Parameterized)
            .Where(candidate => candidate.Length > input.Length &&
                                candidate.StartsWith(input, StringComparison.OrdinalIgnoreCase) &&
                                IsSingleWordStep(input, candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(candidate => candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Length)
            .ThenBy(candidate => candidate.Count(character => character == ' '))
            .ToArray();
    }

    private static bool IsSingleWordStep(string input, string candidate)
    {
        int inputWordCount = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        int candidateWordCount = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (candidateWordCount == inputWordCount)
            return true;

        return candidateWordCount == inputWordCount + 1 &&
               (char.IsWhiteSpace(input[^1]) || candidate.StartsWith(input + " ", StringComparison.OrdinalIgnoreCase));
    }

    private static (string[] Chained, string[] Complete, string[] Parameterized) BuildCandidates()
    {
        var completeCandidates = new List<string>(AdditionalCommands);
        var parameterizedCandidates = new List<string>();
        foreach (var tab in CommandReferenceData.GetTabs())
        {
            if (tab.Header.Equals("Non-Command", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var item in tab.Items)
            {
                string[] aliases = item.Key.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (string alias in aliases
                             .OrderByDescending(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Length ?? 0)
                             .ThenByDescending(value => value.Length))
                {
                    bool hasParameters = item.Key.Contains('[');
                    string command = alias.Split('[', 2)[0].Trim();
                    if (command.Length == 0 || command.StartsWith('+') || command.StartsWith('-'))
                        continue;

                    foreach (string expanded in ExpandOptions(command))
                    {
                        if (!string.IsNullOrWhiteSpace(expanded))
                        {
                            string candidate = Regex.Replace(expanded.Trim(), @"\s+", " ");
                            if (candidate.Equals("ss", StringComparison.OrdinalIgnoreCase) ||
                                candidate.StartsWith("ss ", StringComparison.OrdinalIgnoreCase))
                                continue;

                            (hasParameters ? parameterizedCandidates : completeCandidates).Add(candidate);
                        }
                    }
                }
            }
        }

        completeCandidates.AddRange(CommandContinuations);

        var chainedCandidates = new List<string>();
        foreach (string candidate in completeCandidates)
        {
            string[] words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int length = 1; length < words.Length; length++)
                chainedCandidates.Add(string.Join(' ', words.Take(length)));
        }

        string[] chained = chainedCandidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] complete = completeCandidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] allCandidates = complete.Concat(parameterizedCandidates).ToArray();
        string[] parameterized = parameterizedCandidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(candidate => !allCandidates.Any(other =>
                other.Length > candidate.Length &&
                other.StartsWith(candidate + " ", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return (chained, complete, parameterized);
    }

    private static IEnumerable<string> ExpandOptions(string command)
    {
        var pending = new Queue<string>();
        pending.Enqueue(command);
        while (pending.Count > 0)
        {
            string candidate = pending.Dequeue();
            var match = Regex.Match(candidate, @"(?<left>[^\s/]+)/(?<right>[^\s/]+)|(?<leftSpace>[^\s/]+)\s+/\s+(?<rightSpace>[^\s/]+)");
            if (!match.Success)
            {
                yield return candidate;
                continue;
            }

            string left = match.Groups["left"].Success ? match.Groups["left"].Value : match.Groups["leftSpace"].Value;
            string right = match.Groups["right"].Success ? match.Groups["right"].Value : match.Groups["rightSpace"].Value;
            pending.Enqueue(candidate[..match.Index] + left + candidate[(match.Index + match.Length)..]);
            pending.Enqueue(candidate[..match.Index] + right + candidate[(match.Index + match.Length)..]);
        }
    }
}
