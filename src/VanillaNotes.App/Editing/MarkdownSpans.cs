using System.Text.RegularExpressions;

namespace VanillaNotes.App.Editing;

/// <summary>
/// The parts of a line of Markdown the source box colors as it is typed: headings, emphasis, code,
/// links, quotes, the marks of lists, and the symbols of Markdown themselves. It reads one line at a
/// time, knowing only whether the line is inside a block of code, so that it stays fast on long
/// notes; it is a coloring, not a parser, and a line it reads otherwise than Markdig does is only
/// colored otherwise.
/// </summary>
public static partial class MarkdownSpans
{
    /// <summary>What a part of a line is, which says how it is colored.</summary>
    public enum Kind
    {
        /// <summary>The text of a heading.</summary>
        Heading,

        /// <summary>Text in bold.</summary>
        Strong,

        /// <summary>Text in italics.</summary>
        Emphasis,

        /// <summary>Code, inline or a line of a block of code.</summary>
        Code,

        /// <summary>The address of a link or an image.</summary>
        Link,

        /// <summary>A line of a quote.</summary>
        Quote,

        /// <summary>The mark of an item of a list, or a rule.</summary>
        ListMark,

        /// <summary>A symbol of Markdown itself: the hashes of a heading, the stars of emphasis, the backticks of code.</summary>
        Symbol,
    }

    /// <summary>
    /// The parts of one line, in order, and whether the next line is inside a block of code.
    /// </summary>
    /// <param name="inCode">Whether the line is inside a block of code that a line before it opened.</param>
    public static (IReadOnlyList<Span> Spans, bool InCodeAfter) Of(string line, bool inCode)
    {
        var spans = new List<Span>();
        if (FencePattern().Match(line) is { Success: true } fence)
        {
            spans.Add(new Span(0, line.Length, Kind.Code));
            spans.Add(new Span(fence.Index, fence.Length, Kind.Symbol));
            return (spans, !inCode);
        }

        if (inCode)
        {
            spans.Add(new Span(0, line.Length, Kind.Code));
            return (spans, true);
        }

        if (HeadingPattern().Match(line) is { Success: true } heading)
        {
            spans.Add(new Span(0, line.Length, Kind.Heading));
            spans.Add(new Span(heading.Groups[1].Index, heading.Groups[1].Length, Kind.Symbol));
            return (spans, false);
        }

        if (RulePattern().IsMatch(line))
        {
            spans.Add(new Span(0, line.Length, Kind.ListMark));
            return (spans, false);
        }

        int bodyStart = 0;
        if (QuotePattern().Match(line) is { Success: true } quote)
        {
            spans.Add(new Span(0, line.Length, Kind.Quote));
            spans.Add(new Span(quote.Index, quote.Length, Kind.Symbol));
            bodyStart = quote.Length;
        }

        if (ListPattern().Match(line, bodyStart) is { Success: true } item && item.Index == bodyStart)
        {
            spans.Add(new Span(item.Groups[1].Index, item.Groups[1].Length, Kind.ListMark));
        }

        AddInline(line, spans);
        return (spans, false);
    }

    private static void AddInline(string line, List<Span> spans)
    {
        var code = new List<(int Start, int End)>();
        foreach (Match match in InlineCodePattern().Matches(line))
        {
            spans.Add(new Span(match.Index, match.Length, Kind.Code));
            spans.Add(new Span(match.Index, match.Groups[1].Length, Kind.Symbol));
            spans.Add(new Span(match.Index + match.Length - match.Groups[1].Length, match.Groups[1].Length, Kind.Symbol));
            code.Add((match.Index, match.Index + match.Length));
        }

        bool InCode(int index) => code.Any(range => index >= range.Start && index < range.End);

        foreach (Match match in StrongPattern().Matches(line).Where(match => !InCode(match.Index)))
        {
            spans.Add(new Span(match.Index, match.Length, Kind.Strong));
            spans.Add(new Span(match.Index, 2, Kind.Symbol));
            spans.Add(new Span(match.Index + match.Length - 2, 2, Kind.Symbol));
        }

        foreach (Match match in EmphasisPattern().Matches(line).Where(match => !InCode(match.Index)))
        {
            spans.Add(new Span(match.Groups[1].Index, match.Groups[1].Length, Kind.Emphasis));
            spans.Add(new Span(match.Groups[1].Index, 1, Kind.Symbol));
            spans.Add(new Span(match.Groups[1].Index + match.Groups[1].Length - 1, 1, Kind.Symbol));
        }

        foreach (Match match in LinkPattern().Matches(line).Where(match => !InCode(match.Index)))
        {
            spans.Add(new Span(match.Groups[1].Index, match.Groups[1].Length, Kind.Link));
        }
    }

    /// <summary>One part of a line: where it starts, how long it is, and what it is.</summary>
    public readonly record struct Span(int Start, int Length, Kind Kind);

    [GeneratedRegex(@"^ {0,3}(```|~~~)")]
    private static partial Regex FencePattern();

    [GeneratedRegex(@"^ {0,3}(#{1,6})(?= |$)")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^ {0,3}([-*_])( *\1){2,} *$")]
    private static partial Regex RulePattern();

    [GeneratedRegex(@"^( {0,3}> ?)+")]
    private static partial Regex QuotePattern();

    [GeneratedRegex(@"\G *([-*+]( \[[ xX]\])?|[0-9]{1,9}[.)])(?= )")]
    private static partial Regex ListPattern();

    [GeneratedRegex(@"(`+)[^`].*?\1")]
    private static partial Regex InlineCodePattern();

    [GeneratedRegex(@"(\*\*|__)(?=\S)(.+?)(?<=\S)\1")]
    private static partial Regex StrongPattern();

    [GeneratedRegex(@"(?<![*_\w])((\*|_)(?=[^\s*_])[^*_]*?(?<=[^\s*_])\2)(?![*_\w])")]
    private static partial Regex EmphasisPattern();

    [GeneratedRegex(@"!?\[[^\]]*\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex LinkPattern();
}
