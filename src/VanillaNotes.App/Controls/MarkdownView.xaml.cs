using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using VanillaNotes.App.Editing;

namespace VanillaNotes.App.Controls;

/// <summary>
/// A note formatted, read-only. The document is built again a moment after the text stops
/// changing, so that the preview beside the source keeps up with typing. A link opens in the
/// browser only when it is a web or mail address; this control departs from "no logic in the
/// code-behind" because a control is what it is: the code of its own behavior.
/// </summary>
public partial class MarkdownView
{
    /// <summary>The text of the note, as Markdown.</summary>
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown), typeof(string), typeof(MarkdownView), new PropertyMetadata("", (view, _) => ((MarkdownView)view).Schedule()));

    /// <summary>The picture of an attached image, by its number; with none, an image shows its text.</summary>
    public static readonly DependencyProperty ImageOfProperty = DependencyProperty.Register(
        nameof(ImageOf), typeof(Func<long, ImageSource?>), typeof(MarkdownView), new PropertyMetadata(null, (view, _) => ((MarkdownView)view).Schedule()));

    /// <summary>How long the view waits after the text stops changing before it builds the document again.</summary>
    private static readonly TimeSpan _renderDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>The kinds of address a link opens.</summary>
    private static readonly string[] _openedSchemes = ["http", "https", "mailto"];

    private readonly DispatcherTimer _renderTimer;

    public MarkdownView()
    {
        InitializeComponent();
        _renderTimer = new DispatcherTimer { Interval = _renderDelay };
        _renderTimer.Tick += (_, _) => Render();
        Viewer.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnNavigate));
        Loaded += (_, _) => Render();
    }

    /// <summary>The text of the note.</summary>
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>The picture of an attached image, by its number.</summary>
    public Func<long, ImageSource?>? ImageOf
    {
        get => (Func<long, ImageSource?>?)GetValue(ImageOfProperty);
        set => SetValue(ImageOfProperty, value);
    }

    /// <summary>Whether the theme in use has a dark surface, for the colors of the code.</summary>
    public static bool IsDark() =>
        Application.Current.TryFindResource("Surface") is SolidColorBrush surface
        && ((0.299 * surface.Color.R) + (0.587 * surface.Color.G) + (0.114 * surface.Color.B)) < 128;

    private void Schedule()
    {
        _renderTimer.Stop();
        if (Viewer.Document is null)
        {
            Render();
            return;
        }

        _renderTimer.Start();
    }

    private void Render()
    {
        _renderTimer.Stop();
        Viewer.Document = MarkdownToDocument.Convert(Markdown ?? "", ImageOf, IsDark());
    }

    private static void OnNavigate(object sender, RequestNavigateEventArgs navigation)
    {
        navigation.Handled = true;
        Uri address = navigation.Uri;
        if (address.IsAbsoluteUri && _openedSchemes.Contains(address.Scheme))
        {
            Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
        }
    }
}
