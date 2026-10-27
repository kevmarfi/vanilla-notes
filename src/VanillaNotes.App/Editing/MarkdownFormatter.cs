using VanillaNotes.Core.Markdown;
using System.Text.RegularExpressions;

namespace VanillaNotes.App.Editing;

/// <summary>
/// Writes the Markdown symbols of a format into the source box, around what is selected or at the
/// start of the lines selected, as markdown-format.js of the web version does. It works on the
/// text and the selection, and the box takes back both.
/// </summary>
public static partial class MarkdownFormatter
{
    /// <summary>What a link takes as its text when nothing is selected.</summary>
    private const string _linkText = "link";

    /// <summary>The line that opens and closes a block of code.</summary>
    private const string _codeFence = "```";

    /// <summary>A dividing line, in a paragraph of its own.</summary>
    private const string _divider = "\n\n---\n\n";

    /// <summary>The symbols written around what is selected, by format.</summary>
    private static readonly Dictionary<string, string> _wrappers = new(StringComparer.Ordinal)
    {
        ["bold"] = "**",
        ["italic"] = "*",
        ["strike"] = "~~",
    };

    /// <summary>The mark written at the start of every line selected, by format; a paragraph has none.</summary>
    private static readonly Dictionary<string, string> _linePrefixes = new(StringComparer.Ordinal)
    {
        ["paragraph"] = "",
        ["heading1"] = "# ",
        ["heading2"] = "## ",
        ["bulletList"] = "- ",
        ["orderedList"] = "1. ",
        ["taskList"] = "- [ ] ",
        ["blockquote"] = "> ",
    };

    /// <summary>
    /// Writes a format into a text.
    /// </summary>
    /// <param name="start">Where the selection starts.</param>
    /// <param name="length">How long the selection is; nothing selected is a length of zero.</param>
    /// <param name="href">The address of a link, for the format of a link.</param>
    /// <returns>The text with the format, and where the selection goes in it.</returns>
    public static (string Text, int SelectionStart, int SelectionLength) Apply(string text, int start, int length, string format, string? href = null)
    {
        int end = start + length;
        string selected = text[start..end];
        if (_wrappers.TryGetValue(format, out string? wrapper))
        {
            string wrapped = wrapper + selected + wrapper;

            // With nothing selected the cursor goes between the symbols, ready to type.
            return selected.Length == 0
                ? (Replace(text, start, end, wrapped), start + wrapper.Length, 0)
                : (Replace(text, start, end, wrapped), start + wrapped.Length, 0);
        }

        if (_linePrefixes.ContainsKey(format))
        {
            // The format is of whole lines, so it reaches from the start of the first one to the end of the last one.
            int lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
            int nextBreak = text.IndexOf('\n', end);
            int lineEnd = nextBreak == -1 ? text.Length : nextBreak;
            string[] lines = text[lineStart..lineEnd].Split('\n');
            string marked = string.Join('\n', lines.Select((line, index) => MarkOf(format, index) + LineMark().Replace(line, "", 1)));
            return (Replace(text, lineStart, lineEnd, marked), lineStart + marked.Length, 0);
        }

        switch (format)
        {
            case "codeBlock":
                string block = $"{_codeFence}\n{selected}\n{_codeFence}";
                return selected.Length == 0
                    ? (Replace(text, start, end, block), start + _codeFence.Length + 1, 0)
                    : (Replace(text, start, end, block), start + block.Length, 0);

            case "horizontalRule":
                return (Replace(text, end, end, _divider), end + _divider.Length, 0);

            case "link":
                string link = $"[{(selected.Length == 0 ? _linkText : selected)}]({href})";
                return (Replace(text, start, end, link), start + link.Length, 0);

            default:
                throw new ArgumentException($"Unknown format: {format}", nameof(format));
        }
    }

    /// <summary>
    /// Writes an attached image in place of what is selected, or where the cursor is: the name of
    /// the file is its alternative text, with the brackets that would end that text escaped. A name
    /// holds no backslash, which the validator refuses.
    /// </summary>
    /// <returns>The text with the image, and where the cursor goes: right after it.</returns>
    public static (string Text, int Caret) InsertImage(string text, int start, int length, string fileName, long attachmentId)
    {
        string image = $"![{AltBracket().Replace(fileName, @"\$0")}]({MarkdownParser.AttachmentAddress(attachmentId)})";
        return (Replace(text, start, start + length, image), start + image.Length);
    }

    /// <summary>The mark of a line: a numbered list counts its lines; every other format repeats the same mark.</summary>
    private static string MarkOf(string format, int index) => format == "orderedList" ? $"{index + 1}. " : _linePrefixes[format];

    private static string Replace(string text, int start, int end, string replacement) => text[..start] + replacement + text[end..];

    /// <summary>The mark a line may already start with, which a new one replaces.</summary>
    [GeneratedRegex(@"^(#{1,6} |[-*+] \[[ xX]\] |[-*+] |\d+\. |> )")]
    private static partial Regex LineMark();

    /// <summary>A bracket in the alternative text of an image, which would end that text.</summary>
    [GeneratedRegex(@"[\[\]]")]
    private static partial Regex AltBracket();
}
