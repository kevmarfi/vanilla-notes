using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VanillaNotes.App.Editing;
using VanillaNotes.App.ViewModels;

namespace VanillaNotes.App.Controls;

/// <summary>
/// The visual way of writing: Markdown in, the document of the rich-text editor on screen, and
/// Markdown out a moment after the person stops typing. When the text arrives, it says whether
/// the visual way can write it back without losing anything; a note it would damage is not
/// written back. A paste is always taken as plain text, and Enter after a heading starts a plain
/// paragraph. This control departs from "no logic in the code-behind": what it does is the
/// behavior of the editor itself, and the rules of the round trip are in the converters.
/// </summary>
public partial class VisualEditor
{
    /// <summary>The text of the note, as Markdown, bound in both directions.</summary>
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown), typeof(string), typeof(VisualEditor),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (editor, _) => ((VisualEditor)editor).Load()));

    /// <summary>Whether the spelling of what is written is checked.</summary>
    public static readonly DependencyProperty SpellcheckProperty = DependencyProperty.Register(
        nameof(Spellcheck), typeof(bool), typeof(VisualEditor), new PropertyMetadata(true, (editor, change) => SpellCheck.SetIsEnabled(((VisualEditor)editor).Box, (bool)change.NewValue)));

    /// <summary>Asks the person for the address of a link; nothing when the person cancels.</summary>
    public static readonly DependencyProperty AskLinkProperty = DependencyProperty.Register(nameof(AskLink), typeof(Func<string?>), typeof(VisualEditor));

    /// <summary>Whether the visual way can write the note without losing anything of it.</summary>
    public static readonly DependencyProperty IsVisualAllowedProperty = DependencyProperty.Register(
        nameof(IsVisualAllowed), typeof(bool), typeof(VisualEditor), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>The picture of an attached image, by its number; with none, an image shows its text.</summary>
    public static readonly DependencyProperty ImageOfProperty = DependencyProperty.Register(
        nameof(ImageOf), typeof(Func<long, ImageSource?>), typeof(VisualEditor), new PropertyMetadata(null, (editor, _) => ((VisualEditor)editor).Load()));

    /// <summary>What takes an image pasted, as the bytes of a PNG file, to attach it to the note and write it where the cursor is.</summary>
    public static readonly DependencyProperty PasteImageProperty = DependencyProperty.Register(nameof(PasteImage), typeof(Action<byte[]>), typeof(VisualEditor));

    /// <summary>A number that, each time it changes, tells the editor shown to take the keyboard.</summary>
    public static readonly DependencyProperty FocusRequestProperty = DependencyProperty.Register(
        nameof(FocusRequest), typeof(int), typeof(VisualEditor), new PropertyMetadata(0, (editor, _) => ((VisualEditor)editor).TakeKeyboard()));

    /// <summary>An attached image to put where the cursor is; the editor puts it only while it is the one shown.</summary>
    public static readonly DependencyProperty ImageToInsertProperty = DependencyProperty.Register(
        nameof(ImageToInsert), typeof(AttachmentItemViewModel), typeof(VisualEditor), new PropertyMetadata(null, (editor, change) => ((VisualEditor)editor).Insert(change.NewValue as AttachmentItemViewModel)));

    /// <summary>How long the editor waits after the person stops typing before it writes the Markdown back.</summary>
    private static readonly TimeSpan _writeDelay = TimeSpan.FromMilliseconds(300);

    private readonly DispatcherTimer _writeTimer;

    /// <summary>Whether the document is being filled from the Markdown, which is not the person writing.</summary>
    private bool _isLoading;

    /// <summary>Whether the Markdown is being written back from the document, so that it is not read in again.</summary>
    private bool _isWriting;

    /// <summary>Whether the person did something to the document since it was loaded: only then is it written back, so that opening a note never changes it.</summary>
    private bool _isTouched;

    public VisualEditor()
    {
        InitializeComponent();
        _writeTimer = new DispatcherTimer { Interval = _writeDelay };
        _writeTimer.Tick += (_, _) => WriteBack();
        Box.TextChanged += (_, _) =>
        {
            if (!_isLoading && IsVisualAllowed)
            {
                _writeTimer.Stop();
                _writeTimer.Start();
            }
        };
        Box.LostKeyboardFocus += (_, _) => WriteBack();
        Box.PreviewKeyDown += OnKey;
        Box.PreviewMouseLeftButtonUp += OnClick;
        Box.PreviewTextInput += (_, _) => _isTouched = true;
        DataObject.AddPastingHandler(Box, OnPaste);
        SpellCheck.SetIsEnabled(Box, Spellcheck);
    }

    /// <summary>The text of the note.</summary>
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>Whether the spelling of what is written is checked.</summary>
    public bool Spellcheck
    {
        get => (bool)GetValue(SpellcheckProperty);
        set => SetValue(SpellcheckProperty, value);
    }

    /// <summary>Asks the person for the address of a link.</summary>
    public Func<string?>? AskLink
    {
        get => (Func<string?>?)GetValue(AskLinkProperty);
        set => SetValue(AskLinkProperty, value);
    }

    /// <summary>Whether the visual way can write the note without losing anything of it.</summary>
    public bool IsVisualAllowed
    {
        get => (bool)GetValue(IsVisualAllowedProperty);
        set => SetValue(IsVisualAllowedProperty, value);
    }

    /// <summary>The picture of an attached image, by its number.</summary>
    public Func<long, ImageSource?>? ImageOf
    {
        get => (Func<long, ImageSource?>?)GetValue(ImageOfProperty);
        set => SetValue(ImageOfProperty, value);
    }

    /// <summary>What takes an image pasted, to attach it to the note and write it where the cursor is.</summary>
    public Action<byte[]>? PasteImage
    {
        get => (Action<byte[]>?)GetValue(PasteImageProperty);
        set => SetValue(PasteImageProperty, value);
    }

    /// <summary>The number that, each time it changes, tells the editor shown to take the keyboard.</summary>
    public int FocusRequest
    {
        get => (int)GetValue(FocusRequestProperty);
        set => SetValue(FocusRequestProperty, value);
    }

    /// <summary>An attached image to put where the cursor is.</summary>
    public AttachmentItemViewModel? ImageToInsert
    {
        get => (AttachmentItemViewModel?)GetValue(ImageToInsertProperty);
        set => SetValue(ImageToInsertProperty, value);
    }

    // -- Markdown in and out -- //

    private void Load()
    {
        if (_isWriting)
        {
            return;
        }

        _writeTimer.Stop();
        _isLoading = true;
        string markdown = Markdown ?? "";
        IsVisualAllowed = DocumentToMarkdown.RoundTrips(markdown);
        Box.Document = MarkdownToDocument.Convert(markdown, ImageOf, MarkdownView.IsDark());
        Box.IsReadOnly = !IsVisualAllowed;
        _isTouched = false;
        _isLoading = false;
    }

    private void WriteBack()
    {
        _writeTimer.Stop();
        if (_isLoading || !IsVisualAllowed || !_isTouched)
        {
            return;
        }

        string markdown = DocumentToMarkdown.Convert(Box.Document);
        if (markdown == Markdown)
        {
            return;
        }

        _isWriting = true;
        Markdown = markdown;
        _isWriting = false;
    }

    /// <summary>Takes the keyboard, once the way of writing that was just chosen is laid out, if this is the editor shown.</summary>
    private void TakeKeyboard() => Dispatcher.BeginInvoke(() =>
    {
        if (IsVisible)
        {
            Box.Focus();
        }
    }, DispatcherPriority.Input);

    /// <summary>
    /// A click below the last line, which the box leaves to the area around the text, puts the cursor
    /// at the end of the note. Without it, only the first line of an empty note could be clicked.
    /// A click on the scroll bar is left alone.
    /// </summary>
    private void OnClick(object sender, MouseButtonEventArgs click)
    {
        if (!Box.IsKeyboardFocusWithin && click.GetPosition(Box).X <= Box.ViewportWidth)
        {
            Box.Focus();
            Box.CaretPosition = Box.Document.ContentEnd;
        }
    }

    /// <summary>Puts an attached image where the cursor is, and writes the note back with it.</summary>
    private void Insert(AttachmentItemViewModel? image)
    {
        if (image is null || !IsVisible || Box.IsReadOnly || Box.CaretPosition.Paragraph is null)
        {
            return;
        }

        MarkdownToDocument.InsertAttachedImage(Box.CaretPosition, image.FileName, image.Id, ImageOf ?? (_ => null));
        _isTouched = true;
        WriteBack();
        Box.Focus();
    }

    // -- Keys and the clipboard -- //

    /// <summary>
    /// A paste is plain text: the clipboard formats of the editor would bring blocks it cannot write
    /// back. An image alone, such as a screenshot, is attached to the note instead, and written where the cursor is.
    /// </summary>
    private void OnPaste(object sender, DataObjectPastingEventArgs paste)
    {
        if (PastedImage.PngOf(paste.DataObject) is byte[] png)
        {
            paste.CancelCommand();
            PasteImage?.Invoke(png);
            return;
        }

        _isTouched = true;
        paste.CancelCommand();
        if (paste.DataObject.GetData(DataFormats.UnicodeText) is string text)
        {
            Box.Selection.Text = text;
            Box.CaretPosition = Box.Selection.End;
        }
    }

    private void OnKey(object sender, KeyEventArgs key)
    {
        // Keys that change the text without typing a character: deleting, and breaking a line.
        if (key.Key is Key.Back or Key.Delete or Key.Enter || (Keyboard.Modifiers == ModifierKeys.Control && key.Key is Key.X or Key.B or Key.I or Key.U or Key.Z or Key.Y))
        {
            _isTouched = true;
        }

        if (key.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || Box.CaretPosition.Paragraph is not Paragraph paragraph)
        {
            return;
        }

        // Enter inside a block of code is a new line of the block, not a new paragraph.
        if (paragraph.Tag is string tag && tag.StartsWith(MarkdownToDocument.CodeTag, StringComparison.Ordinal))
        {
            Box.CaretPosition = Box.CaretPosition.InsertLineBreak();
            key.Handled = true;
            return;
        }

        // Enter at the end of a heading starts a plain paragraph, not another heading.
        if (paragraph.Tag is string heading && heading.StartsWith('h') && Box.CaretPosition.GetOffsetToPosition(paragraph.ContentEnd) == 0)
        {
            Paragraph next = MarkdownToDocument.NewParagraph();
            paragraph.SiblingBlocks.InsertAfter(paragraph, next);
            Box.CaretPosition = next.ContentStart;
            key.Handled = true;
        }
    }

    // -- Formats -- //

    private void OnFormat(object sender, RoutedEventArgs click)
    {
        string format = (string)((Button)sender).Tag;
        if (!IsVisualAllowed)
        {
            return;
        }

        _isTouched = true;

        Paragraph? paragraph = Box.CaretPosition.Paragraph;
        switch (format)
        {
            case "bold":
                EditingCommands.ToggleBold.Execute(null, Box);
                break;
            case "italic":
                EditingCommands.ToggleItalic.Execute(null, Box);
                break;
            case "strike":
                bool struck = Box.Selection.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection decorations
                    && decorations.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough);
                Box.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, struck ? new TextDecorationCollection() : TextDecorations.Strikethrough);
                break;
            case "bulletList":
                EditingCommands.ToggleBullets.Execute(null, Box);
                break;
            case "orderedList":
                EditingCommands.ToggleNumbering.Execute(null, Box);
                break;
            case "taskList":
                EditingCommands.ToggleBullets.Execute(null, Box);
                if (Box.CaretPosition.Paragraph is Paragraph task && !new TextRange(task.ContentStart, task.ContentEnd).Text.StartsWith(MarkdownToDocument.UncheckedMark.Trim(), StringComparison.Ordinal))
                {
                    task.ContentStart.InsertTextInRun(MarkdownToDocument.UncheckedMark);
                }

                break;
            case "link":
                InsertLink();
                break;
            case "horizontalRule":
                InsertRule(paragraph);
                break;
            default:
                if (paragraph is not null)
                {
                    SetBlock(paragraph, format);
                }

                break;
        }

        Box.Focus();
        WriteBack();
    }

    /// <summary>Makes the paragraph of the caret a paragraph, a heading, a quote, or a block of code.</summary>
    private static void SetBlock(Paragraph paragraph, string format)
    {
        paragraph.ClearValue(FrameworkContentElement.TagProperty);
        paragraph.ClearValue(TextElement.FontSizeProperty);
        paragraph.ClearValue(TextElement.FontWeightProperty);
        paragraph.ClearValue(TextElement.FontFamilyProperty);
        paragraph.ClearValue(TextElement.BackgroundProperty);
        paragraph.ClearValue(Block.PaddingProperty);
        switch (format)
        {
            case "heading1":
            case "heading2":
                MarkdownToDocument.MakeHeading(paragraph, format == "heading1" ? 1 : 2);
                break;
            case "codeBlock":
                MarkdownToDocument.MakeCode(paragraph, "");
                break;
            case "blockquote":
                Quote(paragraph);
                break;
            default:
                break;
        }
    }

    /// <summary>Puts a paragraph in a quote of its own, or takes it out of the quote it is in.</summary>
    private static void Quote(Paragraph paragraph)
    {
        if (paragraph.Parent is Section { Tag: MarkdownToDocument.QuoteTag } quote)
        {
            quote.Blocks.Remove(paragraph);
            quote.SiblingBlocks.InsertAfter(quote, paragraph);
            if (quote.Blocks.Count == 0)
            {
                quote.SiblingBlocks.Remove(quote);
            }

            return;
        }

        Section section = MarkdownToDocument.NewQuote();
        BlockCollection siblings = paragraph.SiblingBlocks;
        siblings.InsertBefore(paragraph, section);
        siblings.Remove(paragraph);
        section.Blocks.Add(paragraph);
    }

    private void InsertLink()
    {
        string? address = AskLink?.Invoke();
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        TextSelection selection = Box.Selection;
        if (selection.IsEmpty)
        {
            selection.Text = "link";
        }

        _ = new Hyperlink(selection.Start, selection.End) { NavigateUri = new Uri(address.Trim(), UriKind.RelativeOrAbsolute) };
    }

    private void InsertRule(Paragraph? paragraph)
    {
        BlockUIContainer rule = MarkdownToDocument.NewRule();
        Paragraph after = MarkdownToDocument.NewParagraph();
        if (paragraph is null)
        {
            Box.Document.Blocks.Add(rule);
            Box.Document.Blocks.Add(after);
        }
        else
        {
            paragraph.SiblingBlocks.InsertAfter(paragraph, rule);
            rule.SiblingBlocks.InsertAfter(rule, after);
        }

        Box.CaretPosition = after.ContentStart;
    }
}
