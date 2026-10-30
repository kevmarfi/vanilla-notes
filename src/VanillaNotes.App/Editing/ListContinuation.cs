using System.Globalization;
using System.Text.RegularExpressions;

namespace VanillaNotes.App.Editing;

/// <summary>
/// What Enter and Tab do on a line of a list or of a quote in the source box, as editors of
/// Markdown do: Enter starts the next item with the same mark, the next number, or an empty task,
/// inside the same quote; Enter on an item left empty ends the list instead; Tab on an item makes it
/// an item of the one above. It works on the text and the cursor, and the box takes back both.
/// </summary>
public static partial class ListContinuation
{
    /// <summary>What Tab puts before an item to make it an item of the one above: four spaces, which nest under a mark of any width.</summary>
    public const string Indentation = "    ";

    /// <summary>
    /// What Enter does where the cursor is, when the line is an item of a list or a line of a quote
    /// and the cursor is past its mark.
    /// </summary>
    /// <param name="caret">Where the cursor is, with nothing selected.</param>
    /// <returns>The text after Enter and where the cursor goes; nothing when Enter is to do what it always does.</returns>
    public static (string Text, int Caret)? Enter(string text, int caret)
    {
        (int lineStart, int lineEnd) = LineAround(text, caret);
        string line = text[lineStart..lineEnd];
        Match prefix = Prefix().Match(line);
        bool isItem = prefix.Groups["item"].Success;
        if ((!isItem && prefix.Groups["quote"].Length == 0) || caret - lineStart < prefix.Length || ThematicBreak().IsMatch(line))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(line[prefix.Length..]))
        {
            // An item, or a line of a quote, left empty: Enter ends the list or the quote.
            return (text[..lineStart] + text[lineEnd..], lineStart);
        }

        string next = prefix.Groups["indent"].Value + prefix.Groups["quote"].Value + (isItem ? NextMark(prefix) : "");
        int rest = caret;
        while (rest < lineEnd && text[rest] is ' ' or '\t')
        {
            rest++;
        }

        string inserted = NewLine(text) + next;
        return (text[..caret] + inserted + text[rest..], caret + inserted.Length);
    }

    /// <summary>
    /// What Tab does where the cursor is, when the line is an item of a list: the whole line moves
    /// in, whatever place of it the cursor is at.
    /// </summary>
    /// <param name="caret">Where the cursor is, with nothing selected or a selection inside the line.</param>
    /// <returns>The text after Tab and where the cursor goes; nothing when Tab is to do what it always does.</returns>
    public static (string Text, int Caret)? Indent(string text, int caret)
    {
        (int lineStart, int lineEnd) = LineAround(text, caret);
        string line = text[lineStart..lineEnd];
        if (!Prefix().Match(line).Groups["item"].Success || ThematicBreak().IsMatch(line))
        {
            return null;
        }

        return (text[..lineStart] + Indentation + text[lineStart..], caret + Indentation.Length);
    }

    /// <summary>The mark of the item after one: the same mark, the next number, or an empty task.</summary>
    private static string NextMark(Match prefix)
    {
        string space = prefix.Groups["space"].Value;
        string mark = prefix.Groups["number"].Success
            ? (long.Parse(prefix.Groups["number"].Value, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture) + prefix.Groups["delimiter"].Value
            : prefix.Groups["bullet"].Value;
        return mark + space + (prefix.Groups["task"].Success ? "[ ] " : "");
    }

    /// <summary>Where the line the cursor is on starts and ends, without the end of the line.</summary>
    private static (int Start, int End) LineAround(string text, int caret)
    {
        int start = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        int end = text.IndexOf('\n', caret);
        end = end < 0 ? text.Length : end;
        if (end > start && text[end - 1] == '\r')
        {
            end--;
        }

        return (start, end);
    }

    /// <summary>The end of a line the text already uses, so that a new one is written the same way.</summary>
    private static string NewLine(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    /// <summary>The start of a line: its indentation, the marks of the quotes it is in, and the mark of an item with its box of a task.</summary>
    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<quote>(?:>[ \t]?)*)(?<item>(?:(?<bullet>[-*+])|(?<number>\d{1,9})(?<delimiter>[.)]))(?<space>[ \t]+)(?<task>\[[ xX]\][ \t]+)?)?")]
    private static partial Regex Prefix();

    /// <summary>A dividing line, which starts as an item would and is not one.</summary>
    [GeneratedRegex(@"^[ \t]*(?:>[ \t]?)*([-*_])(?:[ \t]*\1){2,}[ \t]*$")]
    private static partial Regex ThematicBreak();
}
