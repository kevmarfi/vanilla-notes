using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace VanillaNotes.App.Controls;

/// <summary>
/// Underlines with a wave, in the color of danger of the theme, the words of the source box the
/// spell checker of Windows does not know. Only the lines on screen are checked, and what a line
/// was found to hold is kept by its text, so that scrolling back costs nothing.
/// </summary>
public sealed class SpellingRenderer : IBackgroundRenderer
{
    /// <summary>How many lines of text are kept checked before the record is started again.</summary>
    private const int _maxRemembered = 2000;

    /// <summary>The height of a wave of the underline, and half its length, in device-independent pixels.</summary>
    private const double _wave = 2;

    private readonly FrameworkElement _themed;

    /// <summary>The words not known in each line checked, by the text of the line.</summary>
    private readonly Dictionary<string, IReadOnlyList<(int Start, int Length)>> _checked = new(StringComparer.Ordinal);

    private WindowsSpelling? _spelling;

    /// <param name="themed">The element whose resources give the colors of the theme in use.</param>
    public SpellingRenderer(FrameworkElement themed)
    {
        _themed = themed;
    }

    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>The spell checker in use, or nothing when the spelling is not checked.</summary>
    public WindowsSpelling? Spelling
    {
        get => _spelling;
        set
        {
            _spelling = value;
            _checked.Clear();
        }
    }

    /// <summary>The word not known that a place of the text falls in, or nothing when the word there is known.</summary>
    public TextSegment? ErrorAt(TextDocument document, int offset)
    {
        DocumentLine line = document.GetLineByOffset(offset);
        foreach ((int start, int length) in ErrorsOf(document.GetText(line)))
        {
            if (offset >= line.Offset + start && offset <= line.Offset + start + length)
            {
                return new TextSegment { StartOffset = line.Offset + start, Length = length };
            }
        }

        return null;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_spelling is null || !textView.VisualLinesValid || _themed.TryFindResource("Danger") is not Brush brush)
        {
            return;
        }

        var pen = new Pen(brush, 1);
        pen.Freeze();
        foreach (VisualLine visual in textView.VisualLines)
        {
            DocumentLine line = visual.FirstDocumentLine;
            foreach ((int start, int length) in ErrorsOf(textView.Document.GetText(line)))
            {
                var segment = new TextSegment { StartOffset = line.Offset + start, Length = length };
                foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    drawingContext.DrawGeometry(null, pen, Wave(rect.BottomLeft, rect.Right));
                }
            }
        }
    }

    private IReadOnlyList<(int Start, int Length)> ErrorsOf(string text)
    {
        if (_spelling is null || text.Length == 0)
        {
            return [];
        }

        if (!_checked.TryGetValue(text, out IReadOnlyList<(int Start, int Length)>? errors))
        {
            if (_checked.Count >= _maxRemembered)
            {
                _checked.Clear();
            }

            errors = _spelling.Errors(text);
            _checked[text] = errors;
        }

        return errors;
    }

    /// <summary>A wave from a point to the right, along the bottom of a word.</summary>
    private static StreamGeometry Wave(Point from, double right)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(new Point(from.X, from.Y - _wave), isFilled: false, isClosed: false);
            bool up = false;
            for (double x = from.X + _wave; x <= right; x += _wave)
            {
                context.LineTo(new Point(x, up ? from.Y - _wave : from.Y), isStroked: true, isSmoothJoin: true);
                up = !up;
            }
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>Forgets what the lines were found to hold, after a word was added to the dictionary.</summary>
    public void Forget() => _checked.Clear();
}
