namespace OpenApiTools.Services;

public static class FuzzyMatcher
{
    public static int LevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
        if (string.IsNullOrEmpty(s2)) return s1.Length;

        var prev = new int[s2.Length + 1];
        var curr = new int[s2.Length + 1];

        for (var j = 0; j <= s2.Length; j++)
            prev[j] = j;

        for (var i = 1; i <= s1.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= s2.Length; j++)
            {
                var cost = char.ToLowerInvariant(s1[i - 1]) == char.ToLowerInvariant(s2[j - 1]) ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[s2.Length];
    }

    public static double Score(string query, string? target)
    {
        if (string.IsNullOrEmpty(target)) return 0;
        if (string.IsNullOrEmpty(query)) return 0;

        query = query.ToLowerInvariant();
        target = target.ToLowerInvariant();

        if (target.Contains(query)) return 1.0;

        if (target.Split([' ', '/', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Any(w => w.StartsWith(query)))
            return 0.85;

        var maxLen = Math.Max(query.Length, target.Length);
        var distance = LevenshteinDistance(query, target);
        return 1.0 - (double)distance / maxLen;
    }

    public static bool Matches(string query, string? target, double threshold = 0.4)
        => Score(query, target) >= threshold;
}