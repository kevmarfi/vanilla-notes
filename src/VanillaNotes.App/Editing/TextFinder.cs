using System.Text;

namespace VanillaNotes.App.Editing;

/// <summary>
/// Finding a text in the Markdown of a note, and putting another in its place, as the find bar of
/// the Markdown way does: from where the cursor is, going round to the start once the end is
/// reached, with or without telling capitals apart.
/// </summary>
public static class TextFinder
{
    /// <summary>
    /// Where the next match of a text is, from a place on, going round to the start; or the last
    /// one that ends before it, going round to the end, when searching backwards.
    /// </summary>
    /// <param name="from">Where to look from: the end of the selection forwards, its start backwards.</param>
    /// <returns>Where the match starts, or nothing when the text is not there or nothing is looked for.</returns>
    public static int? Find(string text, string query, int from, bool matchCase, bool backwards = false)
    {
        if (query.Length == 0)
        {
            return null;
        }

        StringComparison comparison = Comparison(matchCase);
        from = Math.Clamp(from, 0, text.Length);
        int found = backwards ? LastEndingBy(text, query, from, comparison) : text.IndexOf(query, from, comparison);
        if (found < 0)
        {
            found = backwards ? text.LastIndexOf(query, comparison) : text.IndexOf(query, comparison);
        }

        return found < 0 ? null : found;
    }

    /// <summary>Puts a text in place of every match of another.</summary>
    /// <returns>The text with every match replaced, and how many there were.</returns>
    public static (string Text, int Count) ReplaceAll(string text, string query, string replacement, bool matchCase)
    {
        if (query.Length == 0)
        {
            return (text, 0);
        }

        StringComparison comparison = Comparison(matchCase);
        var written = new StringBuilder(text.Length);
        int count = 0;
        int position = 0;
        for (int found = text.IndexOf(query, comparison); found >= 0; found = text.IndexOf(query, position, comparison))
        {
            written.Append(text, position, found - position).Append(replacement);
            position = found + query.Length;
            count++;
        }

        written.Append(text, position, text.Length - position);
        return (written.ToString(), count);
    }

    /// <summary>Whether the selection is a match of the text looked for, so that replacing it replaces that match.</summary>
    public static bool IsMatch(string selected, string query, bool matchCase) =>
        query.Length > 0 && string.Equals(selected, query, Comparison(matchCase));

    private static StringComparison Comparison(bool matchCase) => matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>The last match that ends no later than a place, or none.</summary>
    private static int LastEndingBy(string text, string query, int end, StringComparison comparison)
    {
        int last = -1;
        for (int found = text.IndexOf(query, comparison); found >= 0 && found + query.Length <= end; found = text.IndexOf(query, found + 1, comparison))
        {
            last = found;
        }

        return last;
    }
}
