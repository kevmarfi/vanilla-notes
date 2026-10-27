using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using VanillaNotes.App.Editing;

namespace VanillaNotes.App.Controls;

/// <summary>
/// Colors the Markdown of the source box as it is typed, with the colors of the theme in use: the
/// parts of each line are the ones of the markdown spans, and whether a line is inside a block of
/// code is worked out once from the start of the note, and again only after the text changes.
/// </summary>
public sealed class MarkdownColorizer : DocumentColorizingTransformer
{
    /// <summary>The key of the color of the theme each kind of part takes; a kind not here keeps the color of the text.</summary>
    private static readonly Dictionary<MarkdownSpans.Kind, string> _colors = new()
    {
        [MarkdownSpans.Kind.Heading] = "Accent",
        [MarkdownSpans.Kind.Code] = "TokenString",
        [MarkdownSpans.Kind.Link] = "TokenKeyword",
        [MarkdownSpans.Kind.Quote] = "TokenComment",
        [MarkdownSpans.Kind.ListMark] = "TokenNumber",
        [MarkdownSpans.Kind.Symbol] = "TextMuted",
    };

    private readonly FrameworkElement _themed;

    /// <summary>Whether each line, by its number less one, starts inside a block of code; read up to where it was needed.</summary>
    private readonly List<bool> _inCode = [false];

    private TextDocument? _document;

    /// <param name="themed">The element whose resources give the colors of the theme in use.</param>
    public MarkdownColorizer(FrameworkElement themed)
    {
        _themed = themed;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        TextDocument document = CurrentContext.Document;
        if (!ReferenceEquals(document, _document))
        {
            if (_document is not null)
            {
                _document.Changed -= Forget;
            }

            _document = document;
            _document.Changed += Forget;
            Forget(null, null);
        }

        string text = document.GetText(line);
        (IReadOnlyList<MarkdownSpans.Span> spans, _) = MarkdownSpans.Of(text, InCodeAt(document, line.LineNumber));
        foreach (MarkdownSpans.Span span in spans.Where(span => span.Length > 0))
        {
            int start = line.Offset + span.Start;
            ChangeLinePart(start, start + span.Length, element => Paint(element, span.Kind));
        }
    }

    private void Paint(VisualLineElement element, MarkdownSpans.Kind kind)
    {
        if (_colors.TryGetValue(kind, out string? key) && _themed.TryFindResource(key) is Brush brush)
        {
            element.TextRunProperties.SetForegroundBrush(brush);
        }

        Typeface face = element.TextRunProperties.Typeface;
        if (kind is MarkdownSpans.Kind.Heading or MarkdownSpans.Kind.Strong)
        {
            element.TextRunProperties.SetTypeface(new Typeface(face.FontFamily, face.Style, FontWeights.Bold, face.Stretch));
        }
        else if (kind == MarkdownSpans.Kind.Emphasis)
        {
            element.TextRunProperties.SetTypeface(new Typeface(face.FontFamily, FontStyles.Italic, face.Weight, face.Stretch));
        }
    }

    /// <summary>Whether a line starts inside a block of code, reading the lines before it that were not read yet.</summary>
    private bool InCodeAt(TextDocument document, int lineNumber)
    {
        while (_inCode.Count < lineNumber)
        {
            int read = _inCode.Count;
            (_, bool after) = MarkdownSpans.Of(document.GetText(document.GetLineByNumber(read)), _inCode[read - 1]);
            _inCode.Add(after);
        }

        return _inCode[lineNumber - 1];
    }

    /// <summary>A change of the text can open or close a block of code anywhere after it: what was read is read again.</summary>
    private void Forget(object? sender, DocumentChangeEventArgs? change)
    {
        int keep = change is null || _document is null ? 1 : Math.Max(1, _document.GetLineByOffset(Math.Min(change.Offset, _document.TextLength)).LineNumber);
        if (_inCode.Count > keep)
        {
            _inCode.RemoveRange(keep, _inCode.Count - keep);
        }
    }
}
