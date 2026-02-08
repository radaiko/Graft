namespace Graft.Core.Tui;

public sealed record FuzzyMatch(int Score, List<int> MatchedIndices);

public static class FuzzyMatcher
{
    /// <summary>
    /// Scores a pattern against a candidate string using subsequence matching.
    /// Returns null if the pattern is not a subsequence of the candidate.
    /// </summary>
    public static FuzzyMatch? Match(string pattern, string candidate)
    {
        if (string.IsNullOrEmpty(pattern))
            return new FuzzyMatch(0, []);

        if (string.IsNullOrEmpty(candidate))
            return null;

        var patternLower = pattern.ToLowerInvariant();
        var candidateLower = candidate.ToLowerInvariant();

        var matchedIndices = new List<int>();
        int patternIdx = 0;

        // First pass: find if pattern is a subsequence
        for (int i = 0; i < candidate.Length && patternIdx < pattern.Length; i++)
        {
            if (candidateLower[i] == patternLower[patternIdx])
            {
                matchedIndices.Add(i);
                patternIdx++;
            }
        }

        if (patternIdx != pattern.Length)
            return null;

        var score = CalculateScore(pattern, candidate, matchedIndices);
        return new FuzzyMatch(score, matchedIndices);
    }

    private static int CalculateScore(string pattern, string candidate, List<int> matchedIndices)
    {
        int score = 0;
        for (int i = 0; i < matchedIndices.Count; i++)
        {
            int idx = matchedIndices[i];

            // Base score per matched character
            score += 10;

            // Consecutive bonus: matched chars are adjacent
            if (i > 0 && matchedIndices[i - 1] == idx - 1)
                score += 5;

            // Word boundary bonus: start of string, or preceded by separator
            if (idx == 0 || !char.IsLetterOrDigit(candidate[idx - 1]))
                score += 8;

            // Exact case bonus
            if (pattern[i] == candidate[idx])
                score += 3;
        }

        // Penalty for unmatched candidate characters
        score -= (candidate.Length - matchedIndices.Count);

        return score;
    }

    /// <summary>
    /// Filters and sorts items by fuzzy match score (descending).
    /// </summary>
    public static List<T> Filter<T>(string pattern, IEnumerable<T> items, Func<T, string> getText)
    {
        if (string.IsNullOrEmpty(pattern))
            return items.ToList();

        return items
            .Select(item => (Item: item, Match: Match(pattern, getText(item))))
            .Where(x => x.Match != null)
            .OrderByDescending(x => x.Match!.Score)
            .Select(x => x.Item)
            .ToList();
    }
}
