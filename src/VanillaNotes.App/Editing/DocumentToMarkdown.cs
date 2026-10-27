using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using VanillaNotes.Core.Markdown;

namespace VanillaNotes.App.Editing;

/// <summary>
/// The document of the visual editor back into Markdown, in the one style the application writes.
/// A block is read by its tag; an inline by its formatting, never by its element type, because
/// after an edit the editor leaves properties behind and not the elements it was given. What the
/// visual way edits and what it only shows is decision 7 of the project: a structure it only shows
/// (a table, an image, a rule) is written back as it was read.
/// </summary>
public static partial class DocumentToMarkdown
{
    /// <summary>
    /// Writes a document as Markdown.
    /// </summary>
    public static string Convert(FlowDocument document) =>
        string.Join('\n', RenderBlocks(document.Blocks, loose: true)).TrimEnd() + "\n";

    /// <summary>
    /// Whether the visual way can write a note without losing anything of it: the note is turned
    /// into the document and back, untouched, and the page of both texts must be the same. A note
    /// that fails is opened in source only, so that it is never damaged by being opened.
    /// </summary>
    public static bool RoundTrips(string markdown)
    {
        string back = Convert(MarkdownToDocument.Convert(markdown));
        return Page(markdown) == Page(back);
    }

    /// <summary>The page of a text, with every run of white space as one space: a line broken softly reads as a space.</summary>
    private static string Page(string markdown) => WhiteSpace().Replace(MarkdownHtmlWriter.Write(markdown, KeepImages), " ").Trim();

    /// <summary>Every attached image as an address of its own, so that the comparison of two pages sees the images too.</summary>
    private static string? KeepImages(long id) => $"#{id}";

    // -- Blocks -- //

    private static List<string> RenderBlocks(IEnumerable<Block> blocks, bool loose)
    {
        var lines = new List<string>();
        Block? previous = null;
        foreach (Block block in blocks)
        {
            // Enter inside a block of code may split its paragraph in two: they are one block again here.
            if (previous is Paragraph before && block is Paragraph after && IsCode(before) && Equals(before.Tag, after.Tag))
            {
                lines.RemoveAt(lines.Count - 1);
                lines.AddRange(CodeLines(after));
                lines.Add("```");
                continue;
            }

            // An empty paragraph is room the person left, not a block.
            if (block is Paragraph { Tag: null } empty && IsEmpty(empty))
            {
                continue;
            }

            if (previous is not null && (loose || (previous is Paragraph && block is Paragraph)))
            {
                lines.Add("");
            }

            lines.AddRange(RenderBlock(block));
            previous = block;
        }

        return lines;
    }

    private static IEnumerable<string> RenderBlock(Block block)
    {
        switch (block)
        {
            case Paragraph paragraph when IsCode(paragraph):
                return [$"```{((string)paragraph.Tag)[MarkdownToDocument.CodeTag.Length..]}", .. CodeLines(paragraph), "```"];

            case Paragraph { Tag: string tag } heading when IsHeading(tag):
                string title = RenderInlines(heading.Inlines, inheritedBold: true).Replace("\\\n", " ", StringComparison.Ordinal);
                return [$"{new string('#', tag[1] - '0')} {title}"];

            case Paragraph paragraph:
                return RenderParagraph(paragraph);

            case List list:
                return RenderList(list);

            case Section { Tag: MarkdownToDocument.QuoteTag } quote:
                return RenderBlocks(quote.Blocks, loose: true).Select(line => line.Length == 0 ? ">" : $"> {line}");

            case Section section:
                return RenderBlocks(section.Blocks, loose: true);

            case BlockUIContainer { Tag: MarkdownToDocument.RuleTag }:
                return ["---"];

            case Table table:
                return RenderTable(table);

            default:
                return [];
        }
    }

    private static string[] RenderParagraph(Paragraph paragraph)
    {
        string[] lines = RenderInlines(paragraph.Inlines, InheritsBold(paragraph)).Split('\n');

        // A line that starts with what Markdown reads as the start of a block keeps it as text.
        // A number is kept by escaping the dot after it; every other mark by escaping the mark.
        lines[0] = BlockStart().Replace(lines[0], match => char.IsAsciiDigit(match.Value[0]) ? match.Value + "\\" : "\\" + match.Value, 1);
        return lines;
    }

    private static bool IsCode(Paragraph paragraph) =>
        paragraph.Tag is string tag && tag.StartsWith(MarkdownToDocument.CodeTag, StringComparison.Ordinal);

    private static bool IsHeading(string tag) => tag.Length == 2 && tag[0] == 'h' && tag[1] is >= '1' and <= '6';

    private static bool IsEmpty(Paragraph paragraph) =>
        new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim().Length == 0
        && !paragraph.Inlines.OfType<InlineUIContainer>().Any();

    private static IEnumerable<string> CodeLines(Paragraph paragraph)
    {
        var text = new StringBuilder();
        foreach (Inline inline in paragraph.Inlines)
        {
            text.Append(inline is LineBreak ? "\n" : new TextRange(inline.ContentStart, inline.ContentEnd).Text);
        }

        return text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }

    /// <summary>The cells of a header row are bold because of the row, not because the person made them so.</summary>
    private static bool InheritsBold(Paragraph paragraph) =>
        paragraph.Parent is TableCell { Parent: TableRow row } && row.FontWeight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight();

    private static List<string> RenderList(List list)
    {
        var lines = new List<string>();
        int number = list.StartIndex;
        bool ordered = list.MarkerStyle is TextMarkerStyle.Decimal or TextMarkerStyle.LowerLatin or TextMarkerStyle.UpperLatin
            or TextMarkerStyle.LowerRoman or TextMarkerStyle.UpperRoman;
        foreach (ListItem item in list.ListItems)
        {
            string marker = ordered ? $"{number++}. " : "- ";
            List<string> body = RenderBlocks(item.Blocks, loose: false);
            if (body.Count == 0)
            {
                body.Add("");
            }

            body[0] = TaskMark(body[0]);
            string indent = new(' ', marker.Length);
            lines.AddRange(body.Select((line, index) => index == 0 ? marker + line : line.Length == 0 ? "" : indent + line));
        }

        return lines;
    }

    /// <summary>The box of a task, shown as a mark, written back as Markdown writes it.</summary>
    private static string TaskMark(string line)
    {
        if (line.StartsWith(MarkdownToDocument.CheckedMark, StringComparison.Ordinal))
        {
            return "[x] " + line[MarkdownToDocument.CheckedMark.Length..];
        }

        return line.StartsWith(MarkdownToDocument.UncheckedMark, StringComparison.Ordinal)
            ? "[ ] " + line[MarkdownToDocument.UncheckedMark.Length..]
            : line;
    }

    private static List<string> RenderTable(Table table)
    {
        var lines = new List<string>();
        string[] alignments = (table.Tag as string ?? "").Split(',');
        bool first = true;
        foreach (TableRow row in table.RowGroups.SelectMany(group => group.Rows))
        {
            IEnumerable<string> cells = row.Cells.Select(cell => string.Join(' ', RenderBlocks(cell.Blocks, loose: false))
                .Replace("|", "\\|", StringComparison.Ordinal));
            lines.Add($"| {string.Join(" | ", cells)} |");
            if (first)
            {
                IEnumerable<string> rules = row.Cells.Select((_, index) => (index < alignments.Length ? alignments[index] : "") switch
                {
                    "Left" => ":--",
                    "Center" => ":-:",
                    "Right" => "--:",
                    _ => "---",
                });
                lines.Add($"| {string.Join(" | ", rules)} |");
                first = false;
            }
        }

        return lines;
    }

    // -- Inlines -- //

    /// <summary>A stretch of text with one formatting all along it.</summary>
    private sealed record Piece(string Text, bool Bold, bool Italic, bool Strike, bool Code, string? Link, bool IsRaw);

    /// <summary>The markers of emphasis, in the order they are opened, outermost first.</summary>
    private enum Marker
    {
        /// <summary>Two asterisks.</summary>
        Bold,

        /// <summary>One asterisk.</summary>
        Italic,

        /// <summary>Two tildes.</summary>
        Strike,
    }

    private static string RenderInlines(InlineCollection inlines, bool inheritedBold)
    {
        var pieces = new List<Piece>();
        Collect(inlines, inheritedBold, strike: false, link: null, pieces);
        var output = new StringBuilder();
        int start = 0;

        // A link wraps the stretch of pieces that point at it; emphasis is nested inside each stretch.
        while (start < pieces.Count)
        {
            int end = start;
            while (end + 1 < pieces.Count && pieces[end + 1].Link == pieces[start].Link)
            {
                end++;
            }

            string stretch = Emphasize(pieces.GetRange(start, end - start + 1));
            output.Append(pieces[start].Link is string link && stretch.Trim().Length > 0 ? $"[{stretch}]({link})" : stretch);
            start = end + 1;
        }

        return output.ToString();
    }

    private static void Collect(InlineCollection inlines, bool inheritedBold, bool strike, string? link, List<Piece> pieces)
    {
        foreach (Inline inline in inlines)
        {
            // Unlike weight and style, a decoration is not inherited by the runs inside: it is carried down here.
            bool struck = strike || (inline.TextDecorations?.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough) ?? false);
            switch (inline)
            {
                case Run run:
                    pieces.Add(new Piece(
                        run.Text,
                        Bold: !inheritedBold && run.FontWeight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight(),
                        Italic: run.FontStyle == FontStyles.Italic,
                        Strike: struck,
                        Code: run.FontFamily.Source == MarkdownToDocument.MonoFont.Source,
                        link,
                        IsRaw: false));
                    break;

                case LineBreak:
                    pieces.Add(new Piece("\\\n", false, false, false, false, link, IsRaw: true));
                    break;

                case InlineUIContainer { Tag: string tag }:
                    string[] parts = tag.Split('\n');
                    pieces.Add(new Piece($"![{AltBracket().Replace(parts[0], @"\$0")}]({parts[1]})", false, false, false, false, link, IsRaw: true));
                    break;

                case Hyperlink hyperlink:
                    Collect(hyperlink.Inlines, inheritedBold, struck, hyperlink.NavigateUri?.OriginalString ?? "", pieces);
                    break;

                case Span span:
                    Collect(span.Inlines, inheritedBold, struck, link, pieces);
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Writes pieces of text with their emphasis nested: a marker stays open across the pieces that
    /// share it, and is closed when a piece no longer has it, after the ones opened inside it.
    /// Markers touch the text: the spaces at the ends of a piece stay outside them.
    /// </summary>
    private static string Emphasize(List<Piece> pieces)
    {
        var output = new StringBuilder();
        var open = new List<Marker>();
        foreach (Piece piece in pieces)
        {
            if (piece.IsRaw)
            {
                Close(output, open, 0);
                output.Append(piece.Text);
                continue;
            }

            string core = piece.Text.Trim();
            if (core.Length == 0)
            {
                output.Append(piece.Text);
                continue;
            }

            var wanted = new List<Marker>();
            if (piece.Bold)
            {
                wanted.Add(Marker.Bold);
            }

            if (piece.Italic)
            {
                wanted.Add(Marker.Italic);
            }

            if (piece.Strike)
            {
                wanted.Add(Marker.Strike);
            }

            // Close from the first marker that is open and no longer wanted, and every marker inside it.
            int keep = open.FindIndex(marker => !wanted.Contains(marker));
            string leading = piece.Text[..(piece.Text.Length - piece.Text.TrimStart().Length)];
            string trailing = piece.Text[piece.Text.TrimEnd().Length..];
            if (keep >= 0)
            {
                Close(output, open, keep);
            }

            output.Append(leading);
            foreach (Marker marker in wanted.Where(marker => !open.Contains(marker)))
            {
                output.Append(Symbol(marker));
                open.Add(marker);
            }

            output.Append(piece.Code ? Code(core) : Escape(core));
            output.Append(trailing);
        }

        Close(output, open, 0);
        return output.ToString();
    }

    /// <summary>Closes the markers open from a position on, innermost first, before any space the text ended with.</summary>
    private static void Close(StringBuilder output, List<Marker> open, int from)
    {
        if (open.Count <= from)
        {
            return;
        }

        int spaces = 0;
        while (spaces < output.Length && output[output.Length - 1 - spaces] == ' ')
        {
            spaces++;
        }

        output.Length -= spaces;
        for (int i = open.Count - 1; i >= from; i--)
        {
            output.Append(Symbol(open[i]));
            open.RemoveAt(i);
        }

        output.Append(' ', spaces);
    }

    private static string Symbol(Marker marker) => marker switch
    {
        Marker.Bold => "**",
        Marker.Italic => "*",
        _ => "~~",
    };

    /// <summary>Code inside a line, with as many backticks around it as it needs to hold its own.</summary>
    private static string Code(string code)
    {
        string fence = code.Contains('`', StringComparison.Ordinal) ? "``" : "`";
        string padding = code.StartsWith('`') || code.EndsWith('`') ? " " : "";
        return $"{fence}{padding}{code}{padding}{fence}";
    }

    /// <summary>Text the person wrote, with every character Markdown would read as a symbol kept as text.</summary>
    private static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (character is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '~')
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        return escaped.ToString();
    }

    /// <summary>A bracket in the alternative text of an image, which would end that text.</summary>
    [GeneratedRegex(@"[\[\]]")]
    private static partial Regex AltBracket();

    /// <summary>Any run of white space.</summary>
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    /// <summary>What Markdown reads as the start of a heading, a quote, a list, or a rule, at the start of a line.</summary>
    [GeneratedRegex(@"^(#{1,6}(?= |$)|>|[-+](?= )|\d+(?=[.)] ))")]
    private static partial Regex BlockStart();
}
