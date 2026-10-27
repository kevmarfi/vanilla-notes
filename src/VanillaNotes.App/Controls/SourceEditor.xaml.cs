using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using VanillaNotes.App.Editing;
using VanillaNotes.App.ViewModels;

namespace VanillaNotes.App.Controls;

/// <summary>
/// The Markdown source of a note, in the editor of AvalonEdit: the Markdown colored as it is typed,
/// the lines numbered, the words the spell checker of Windows does not know underlined with its
/// suggestions in the menu of the right button, a bar to find and replace, a toolbar that writes the
/// symbols of a format, and the note formatted beside it. All of it works on the text and the
/// selection of the editor, which is why this control departs from "no logic in the code-behind":
/// it is the behavior of the control itself, and the rules of formats, of finding, and of coloring
/// are in Editing.
/// </summary>
public partial class SourceEditor
{
    /// <summary>The text of the note, bound in both directions.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SourceEditor), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (editor, change) => ((SourceEditor)editor).Show((string)change.NewValue)));

    /// <summary>Whether the spelling of what is written is checked.</summary>
    public static readonly DependencyProperty SpellcheckProperty = DependencyProperty.Register(
        nameof(Spellcheck), typeof(bool), typeof(SourceEditor), new PropertyMetadata(true, (editor, _) => ((SourceEditor)editor).UseSpelling()));

    /// <summary>Asks the person for the address of a link; nothing when the person cancels.</summary>
    public static readonly DependencyProperty AskLinkProperty = DependencyProperty.Register(nameof(AskLink), typeof(Func<string?>), typeof(SourceEditor));

    /// <summary>The picture of an attached image, by its number, for the preview.</summary>
    public static readonly DependencyProperty ImageOfProperty = DependencyProperty.Register(nameof(ImageOf), typeof(Func<long, ImageSource?>), typeof(SourceEditor));

    /// <summary>What takes an image pasted, as the bytes of a PNG file, to attach it to the note and write it where the cursor is.</summary>
    public static readonly DependencyProperty PasteImageProperty = DependencyProperty.Register(nameof(PasteImage), typeof(Action<byte[]>), typeof(SourceEditor));

    /// <summary>An attached image to write where the cursor is; the box writes it only while it is the one shown.</summary>
    public static readonly DependencyProperty ImageToInsertProperty = DependencyProperty.Register(
        nameof(ImageToInsert), typeof(AttachmentItemViewModel), typeof(SourceEditor), new PropertyMetadata(null, (editor, change) => ((SourceEditor)editor).Insert(change.NewValue as AttachmentItemViewModel)));

    /// <summary>The color of the text of the theme, followed so that the coloring is drawn again when the theme changes.</summary>
    private static readonly DependencyProperty _themeTextProperty = DependencyProperty.Register(
        "ThemeText", typeof(Brush), typeof(SourceEditor), new PropertyMetadata(null, (editor, _) => ((SourceEditor)editor).Source.TextArea.TextView.Redraw()));

    private readonly SpellingRenderer _spelling;

    /// <summary>The menu of the right button, whose first items are the suggestions put in when it opens.</summary>
    private readonly ContextMenu _menu;

    /// <summary>The item of the menu that adds the word not known to the dictionary, and the line after it.</summary>
    private readonly MenuItem _addWord;

    private readonly Separator _spellingEnd;

    /// <summary>The word not known the menu was opened on, or nothing.</summary>
    private TextSegment? _error;

    /// <summary>Whether the text of the editor is being set from the note, which is not the person writing.</summary>
    private bool _isShowing;

    public SourceEditor()
    {
        InitializeComponent();
        Source.Options.EnableHyperlinks = false;
        Source.Options.EnableEmailHyperlinks = false;
        Source.Options.AllowScrollBelowDocument = false;
        Source.TextArea.SetResourceReference(TextArea.SelectionBrushProperty, "AccentSoft");
        Source.TextArea.SelectionForeground = null;
        Source.TextArea.SelectionBorder = null;
        Source.TextArea.Caret.CaretBrush = null;
        SetResourceReference(_themeTextProperty, "Text");
        Source.TextArea.TextView.LineTransformers.Add(new MarkdownColorizer(this));
        _spelling = new SpellingRenderer(this);
        Source.TextArea.TextView.BackgroundRenderers.Add(_spelling);
        Source.TextChanged += (_, _) => Written();
        Source.TextArea.CommandBindings.Insert(0, new CommandBinding(ApplicationCommands.Paste, OnPasteImage, CanPasteImage));
        _menu = (ContextMenu)Resources["EditorMenu"];
        _addWord = (MenuItem)_menu.Items[0];
        _spellingEnd = (Separator)_menu.Items[1];
        _addWord.Click += (_, _) => AddWord();
        foreach (MenuItem item in _menu.Items.OfType<MenuItem>().Where(item => item.Command is not null))
        {
            item.CommandTarget = Source.TextArea;
        }

        Source.TextArea.ContextMenu = _menu;
        Source.TextArea.ContextMenuOpening += OnContextMenu;
        Source.PreviewKeyDown += OnEditorKey;
        InputLanguageManager.Current.InputLanguageChanged += (_, _) => UseSpelling();
        UseSpelling();
    }

    /// <summary>The text of the note.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
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

    /// <summary>The picture of an attached image, by its number.</summary>
    public Func<long, ImageSource?>? ImageOf
    {
        get => (Func<long, ImageSource?>?)GetValue(ImageOfProperty);
        set => SetValue(ImageOfProperty, value);
    }

    /// <summary>An attached image to write where the cursor is.</summary>
    public AttachmentItemViewModel? ImageToInsert
    {
        get => (AttachmentItemViewModel?)GetValue(ImageToInsertProperty);
        set => SetValue(ImageToInsertProperty, value);
    }

    /// <summary>What takes an image pasted, to attach it to the note and write it where the cursor is.</summary>
    public Action<byte[]>? PasteImage
    {
        get => (Action<byte[]>?)GetValue(PasteImageProperty);
        set => SetValue(PasteImageProperty, value);
    }

    // -- The text -- //

    /// <summary>Shows the text of the note when it comes from the note, not from what was typed here.</summary>
    private void Show(string text)
    {
        if (Source.Text == text)
        {
            return;
        }

        _isShowing = true;
        Source.Text = text;
        _isShowing = false;
    }

    private void Written()
    {
        if (!_isShowing)
        {
            Text = Source.Text;
        }
    }

    /// <summary>
    /// Puts a text in place of the one of the editor by changing only the part that differs, so that
    /// undoing takes back that change alone and the rest of the note stays where it was.
    /// </summary>
    private void Write(string text)
    {
        string old = Source.Text;
        int start = 0;
        while (start < old.Length && start < text.Length && old[start] == text[start])
        {
            start++;
        }

        int end = 0;
        while (end < old.Length - start && end < text.Length - start && old[old.Length - 1 - end] == text[text.Length - 1 - end])
        {
            end++;
        }

        Source.Document.Replace(start, old.Length - start - end, text.Substring(start, text.Length - start - end));
    }

    private void Insert(AttachmentItemViewModel? image)
    {
        if (image is null || !IsVisible)
        {
            return;
        }

        (string text, int caret) = MarkdownFormatter.InsertImage(Source.Text, Source.SelectionStart, Source.SelectionLength, image.FileName, image.Id);
        Write(text);
        Source.Select(caret, 0);
        Source.Focus();
    }

    private void OnFormat(object sender, RoutedEventArgs click)
    {
        string format = (string)((Button)sender).Tag;
        string? href = null;
        if (format == "link")
        {
            href = AskLink?.Invoke();
            if (string.IsNullOrWhiteSpace(href))
            {
                return;
            }
        }

        (string text, int start, int length) = MarkdownFormatter.Apply(Source.Text, Source.SelectionStart, Source.SelectionLength, format, href);
        Write(text);
        Source.Select(start, length);
        Source.Focus();
    }

    // -- Pasting -- //

    /// <summary>An image alone on the clipboard, such as a screenshot, is pasted as an attachment; anything else is left to the editor, which pastes text.</summary>
    private void CanPasteImage(object sender, CanExecuteRoutedEventArgs can) =>
        can.CanExecute = PasteImage is not null && Clipboard.GetDataObject() is IDataObject data && PastedImage.PngOf(data) is not null;

    private void OnPasteImage(object sender, ExecutedRoutedEventArgs paste)
    {
        if (Clipboard.GetDataObject() is IDataObject data && PastedImage.PngOf(data) is byte[] png)
        {
            PasteImage?.Invoke(png);
        }
    }

    // -- Spelling -- //

    /// <summary>
    /// Checks the spelling in the language of the keyboard in use, as the text boxes of Windows do;
    /// when Windows does not tell it, which happens, in the language of the regional format, and then
    /// of the display of Windows. Or not at all, when the setting says so.
    /// </summary>
    private void UseSpelling()
    {
        string keyboard = InputLanguageManager.Current.CurrentInputLanguage?.IetfLanguageTag ?? "";
        _spelling.Spelling = Spellcheck
            ? new[] { keyboard, CultureInfo.CurrentCulture.Name, CultureInfo.CurrentUICulture.Name }
                .Where(tag => tag.Length > 0)
                .Select(WindowsSpelling.For)
                .FirstOrDefault(spelling => spelling is not null)
            : null;
        Source.TextArea.TextView.InvalidateLayer(_spelling.Layer);
    }

    /// <summary>
    /// Fills the menu of the right button: on a word not known, its suggestions and adding it to the
    /// dictionary come before cutting, copying, and pasting.
    /// </summary>
    private void OnContextMenu(object sender, ContextMenuEventArgs opening)
    {
        while (_menu.Items[0] != _addWord)
        {
            _menu.Items.RemoveAt(0);
        }

        _error = _spelling.Spelling is not null && Source.GetPositionFromPoint(Mouse.GetPosition(Source)) is TextViewPosition position
            ? _spelling.ErrorAt(Source.Document, Source.Document.GetOffset(position.Location))
            : null;
        _addWord.Visibility = _spellingEnd.Visibility = _error is null ? Visibility.Collapsed : Visibility.Visible;
        if (_error is not TextSegment error || _spelling.Spelling is not WindowsSpelling spelling)
        {
            return;
        }

        int index = 0;
        foreach (string suggestion in spelling.Suggestions(Source.Document.GetText(error)).Take(5))
        {
            var item = new MenuItem { Header = suggestion, FontWeight = FontWeights.SemiBold };
            item.Click += (_, _) => Source.Document.Replace(error, suggestion);
            _menu.Items.Insert(index++, item);
        }
    }

    private void AddWord()
    {
        if (_error is TextSegment error && _spelling.Spelling is WindowsSpelling spelling)
        {
            spelling.Add(Source.Document.GetText(error));
            _spelling.Forget();
            Source.TextArea.TextView.InvalidateLayer(_spelling.Layer);
        }
    }

    // -- Finding and replacing -- //

    /// <summary>The keys of finding, in the editor: Ctrl+F and Ctrl+H open the bar, F3 goes to the next match, Shift+F3 to the one before.</summary>
    private void OnEditorKey(object sender, KeyEventArgs key)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && key.Key is Key.F or Key.H)
        {
            OpenFind();
            key.Handled = true;
        }
        else if (key.Key == Key.F3 && FindBox.Text.Length > 0)
        {
            Find(backwards: Keyboard.Modifiers == ModifierKeys.Shift);
            key.Handled = true;
        }
    }

    /// <summary>The keys of the bar: Enter finds the next match, Shift+Enter the one before, Escape closes the bar.</summary>
    private void OnFindKey(object sender, KeyEventArgs key)
    {
        if (key.Key == Key.Escape)
        {
            CloseFind();
            key.Handled = true;
        }
        else if (key.Key == Key.Enter && Keyboard.FocusedElement == FindBox)
        {
            Find(backwards: Keyboard.Modifiers == ModifierKeys.Shift);
            key.Handled = true;
        }
        else if (key.Key == Key.Enter && Keyboard.FocusedElement == ReplaceBox)
        {
            Replace();
            key.Handled = true;
        }
    }

    private void OnOpenFind(object sender, RoutedEventArgs click) => OpenFind();

    private void OnCloseFind(object sender, RoutedEventArgs click) => CloseFind();

    private void OnFindNext(object sender, RoutedEventArgs click) => Find(backwards: false);

    private void OnFindPrevious(object sender, RoutedEventArgs click) => Find(backwards: true);

    private void OnReplace(object sender, RoutedEventArgs click) => Replace();

    private void OnQueryChanged(object sender, TextChangedEventArgs change) => Tell(notFound: false, replaced: null);

    /// <summary>Says, beside the bar, that nothing was found, or how many matches were replaced; or nothing.</summary>
    private void Tell(bool notFound, int? replaced)
    {
        NotFound.Visibility = notFound ? Visibility.Visible : Visibility.Collapsed;
        Replaced.Visibility = replaced is null ? Visibility.Collapsed : Visibility.Visible;
        ReplacedCount.Text = replaced?.ToString(CultureInfo.CurrentCulture) ?? "";
    }

    /// <summary>Opens the bar with the text selected, when it is on one line, as what to find.</summary>
    private void OpenFind()
    {
        string selected = Source.SelectedText;
        if (selected.Length > 0 && !selected.Contains('\n'))
        {
            FindBox.Text = selected;
        }

        FindBar.Visibility = Visibility.Visible;
        FindBox.Focus();
        FindBox.SelectAll();
    }

    private void CloseFind()
    {
        FindBar.Visibility = Visibility.Collapsed;
        Tell(notFound: false, replaced: null);
        Source.Focus();
    }

    /// <summary>Selects the next match, or the one before, and brings it into view.</summary>
    private void Find(bool backwards)
    {
        int from = backwards ? Source.SelectionStart : Source.SelectionStart + Source.SelectionLength;
        if (TextFinder.Find(Source.Text, FindBox.Text, from, MatchCase.IsChecked == true, backwards) is not int found)
        {
            Tell(notFound: FindBox.Text.Length > 0, replaced: null);
            return;
        }

        Tell(notFound: false, replaced: null);
        Source.Select(found, FindBox.Text.Length);
        Source.ScrollTo(Source.Document.GetLineByOffset(found).LineNumber, 0);
    }

    /// <summary>Puts the replacement in place of the match selected, and goes to the next one; with no match selected, it goes to one first.</summary>
    private void Replace()
    {
        bool matchCase = MatchCase.IsChecked == true;
        if (TextFinder.IsMatch(Source.SelectedText, FindBox.Text, matchCase))
        {
            Source.Document.Replace(Source.SelectionStart, Source.SelectionLength, ReplaceBox.Text);
            Source.Select(Source.SelectionStart + ReplaceBox.Text.Length, 0);
        }

        Find(backwards: false);
    }

    private void OnReplaceAll(object sender, RoutedEventArgs click)
    {
        (string text, int count) = TextFinder.ReplaceAll(Source.Text, FindBox.Text, ReplaceBox.Text, MatchCase.IsChecked == true);
        if (count > 0)
        {
            Write(text);
        }

        Tell(notFound: false, replaced: count);
    }
}
