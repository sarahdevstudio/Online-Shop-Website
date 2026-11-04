namespace Shop.Application.Search;

/// <summary>Typo tolerance for the catalog search: the optimal-string-alignment (Damerau–Levenshtein) distance, cut off early.</summary>
public static class EditDistance
{
    /// <summary>
    /// The number of single-character insertions, deletions, substitutions and adjacent swaps between the two strings, or
    /// <c>max + 1</c> as soon as it is certain to be more than <paramref name="max"/> (words are short, so this is cheap).
    /// </summary>
    public static int Osa(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max)
        {
            return max + 1;
        }

        var previous2 = new int[b.Length + 1];
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    value = Math.Min(value, previous2[j - 2] + 1);
                }

                current[j] = value;
                rowMin = Math.Min(rowMin, value);
            }

            if (rowMin > max)
            {
                return max + 1;
            }

            (previous2, previous, current) = (previous, current, previous2);
        }

        return Math.Min(previous[b.Length], max + 1);
    }

    /// <summary>How many typos a word of this length may contain and still count as the same word: none for 1-2 letters, one up to 5, two beyond.</summary>
    public static int Allowed(int length) => length <= 2 ? 0 : length <= 5 ? 1 : 2;
}
