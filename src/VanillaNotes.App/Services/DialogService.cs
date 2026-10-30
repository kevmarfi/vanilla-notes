using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using VanillaNotes.App.ViewModels;
using VanillaNotes.App.Views;
using VanillaNotes.Core.Models;

namespace VanillaNotes.App.Services;

/// <summary>
/// The one door to the window of the application and to what Windows offers around it: it shows
/// the page of a view model in the window, opens the dialog of a view model over it, closes it,
/// asks a question, copies text, saves a file the person picks, and opens the mail program. A
/// view model asks for these and never touches the window. A dialog is drawn inside the window,
/// over its page, as the web version draws its dialogs, and is not a window of its own; opening
/// one still waits until it is closed, as a dialog window would, so that a view model asks and
/// reads the answer on the next line. The window opens where it was when it last closed, as long
/// as enough of it would still be on a screen.
/// </summary>
public sealed class DialogService
{
    /// <summary>How much of the top of the window must fall on a screen for it to open where it was: enough to take it by its title.</summary>
    private const double _minVisibleWidth = 100;

    /// <summary>How tall the strip at the top of the window is that must fall on a screen.</summary>
    private const double _minVisibleHeight = 32;

    private readonly LanguageService _texts;

    /// <summary>Gives where the window was when it last closed, if it is known.</summary>
    private readonly Func<WindowPlacement?> _lastPlacement;

    /// <summary>Keeps where the window is as it closes.</summary>
    private readonly Action<WindowPlacement> _keepPlacement;

    private readonly ShellViewModel _shell = new();

    /// <summary>The wait of each dialog open, which ends when the dialog is closed.</summary>
    private readonly Dictionary<DialogLayerViewModel, DispatcherFrame> _waits = [];

    private ShellWindow? _window;

    /// <summary>What to do once the page shown is replaced, or the window closed.</summary>
    private Action? _onPageClosed;

    public DialogService(LanguageService texts, Func<WindowPlacement?> lastPlacement, Action<WindowPlacement> keepPlacement)
    {
        _texts = texts;
        _lastPlacement = lastPlacement;
        _keepPlacement = keepPlacement;
    }

    /// <summary>
    /// Shows the page of a view model in the window, in place of the one shown, which is closed;
    /// the window is opened if it is not yet.
    /// </summary>
    /// <param name="onClosed">What to do once the page is closed, by another taking its place or by the window closing.</param>
    public void Show(object viewModel, Action? onClosed = null)
    {
        Action? previous = _onPageClosed;
        _onPageClosed = onClosed;
        _shell.Page = viewModel;
        previous?.Invoke();
        OpenWindow().Activate();
    }

    /// <summary>
    /// Opens the dialog of a view model over the page, in front of any other dialog, and waits
    /// until it is closed. The keyboard goes back where it was.
    /// </summary>
    /// <returns>Whether the dialog was closed with its action done, rather than cancelled.</returns>
    public bool ShowDialog(object viewModel)
    {
        OpenWindow();
        IInputElement? focused = Keyboard.FocusedElement;
        var layer = new DialogLayerViewModel(viewModel);
        var wait = new DispatcherFrame();
        _waits[layer] = wait;
        _shell.Push(layer);
        Dispatcher.PushFrame(wait);
        if (focused is not null)
        {
            Keyboard.Focus(focused);
        }

        return layer.IsDone;
    }

    /// <summary>
    /// Closes the dialog of a view model; or the window, when the view model is its page. A
    /// page that was already replaced has nothing left to close.
    /// </summary>
    /// <param name="done">For a dialog: whether its action was done, rather than cancelled.</param>
    public void Close(object viewModel, bool done = false)
    {
        DialogLayerViewModel? layer = _shell.Dialogs.FirstOrDefault(open => ReferenceEquals(open.Dialog, viewModel));
        if (layer is not null)
        {
            layer.IsDone = done;
            _shell.Remove(layer);
            _waits.Remove(layer, out DispatcherFrame? wait);
            wait!.Continue = false;
            return;
        }

        if (ReferenceEquals(_shell.Page, viewModel))
        {
            _window?.Close();
        }
    }

    /// <summary>
    /// Closes every dialog open, as if each were cancelled, and shows the page of a view model in
    /// place of the one shown once they are gone: a dialog waits inside the page it was opened from,
    /// so the page is replaced only after every wait has ended.
    /// </summary>
    public void CloseAllAndShow(object viewModel)
    {
        foreach (DialogLayerViewModel layer in _shell.Dialogs.ToList())
        {
            Close(layer.Dialog);
        }

        Dispatcher.CurrentDispatcher.InvokeAsync(() => Show(viewModel), DispatcherPriority.Background);
    }

    /// <summary>
    /// Asks the person a question that is answered by going on or not.
    /// </summary>
    /// <param name="confirmKey">The key of the text of the button that goes on.</param>
    /// <returns>Whether the person chose to go on.</returns>
    public bool Confirm(string text, string confirmKey = "dialog.confirm", bool isDanger = false) =>
        ShowDialog(new MessageViewModel(this, text, _texts[confirmKey], _texts["dialog.cancel"], isDanger, hasInput: false));

    /// <summary>Tells the person something, with a single button to close it.</summary>
    public void Notify(string text) => ShowDialog(new MessageViewModel(this, text, _texts["dialog.ok"], cancelText: null, isDanger: false, hasInput: false));

    /// <summary>
    /// Asks the person to type an answer.
    /// </summary>
    /// <returns>What was typed, or nothing when the person cancelled.</returns>
    public string? Ask(string text)
    {
        var question = new MessageViewModel(this, text, _texts["dialog.ok"], _texts["dialog.cancel"], isDanger: false, hasInput: true);
        return ShowDialog(question) ? question.Input : null;
    }

    /// <summary>Puts a text on the clipboard of Windows.</summary>
    public void CopyText(string text) => Clipboard.SetText(text);

    /// <summary>
    /// Saves a text in a file the person picks.
    /// </summary>
    /// <returns>Whether it was saved, rather than the picking cancelled.</returns>
    public bool SaveText(string suggestedName, string text)
    {
        var picker = new SaveFileDialog { FileName = suggestedName, DefaultExt = ".txt", Filter = "Text|*.txt" };
        if (picker.ShowDialog(_window) != true)
        {
            return false;
        }

        File.WriteAllText(picker.FileName, text);
        return true;
    }

    /// <summary>
    /// Saves bytes in a file the person picks, under the name suggested or another.
    /// </summary>
    /// <returns>Whether it was saved, rather than the picking cancelled.</returns>
    public bool SaveFile(string suggestedName, byte[] bytes)
    {
        string extension = Path.GetExtension(suggestedName);
        var picker = new SaveFileDialog { FileName = suggestedName, DefaultExt = extension, Filter = extension.Length > 0 ? $"{extension}|*{extension}" : "" };
        if (picker.ShowDialog(_window) != true)
        {
            return false;
        }

        File.WriteAllBytes(picker.FileName, bytes);
        return true;
    }

    /// <summary>
    /// Asks the person for a file of some kinds, and reads it. A file larger than what the caller
    /// takes is read only up to one byte past it: enough to tell it is too large.
    /// </summary>
    /// <param name="kindName">What the kinds of file are called, as the picker shows it.</param>
    /// <param name="extensions">The endings of the names of the files offered, with their dot.</param>
    /// <param name="maxBytes">The most the caller takes.</param>
    /// <returns>The file picked, or nothing when the person cancelled.</returns>
    public PickedFile? PickFile(string kindName, IEnumerable<string> extensions, long maxBytes)
    {
        var picker = new OpenFileDialog { Filter = $"{kindName}|{string.Join(";", extensions.Select(extension => "*" + extension))}" };
        if (picker.ShowDialog(_window) != true)
        {
            return null;
        }

        using FileStream stream = File.OpenRead(picker.FileName);
        byte[] bytes = new byte[Math.Min(stream.Length, maxBytes + 1)];
        stream.ReadExactly(bytes);
        return new PickedFile(Path.GetFileName(picker.FileName), bytes);
    }

    /// <summary>
    /// Reads the files the person dragged into the window, each up to one byte past the most the
    /// caller takes, as a file picked is read. A folder among them is left out.
    /// </summary>
    public IReadOnlyList<PickedFile> ReadFiles(IEnumerable<string> paths, long maxBytes)
    {
        var files = new List<PickedFile>();
        foreach (string path in paths.Where(File.Exists))
        {
            using FileStream stream = File.OpenRead(path);
            byte[] bytes = new byte[Math.Min(stream.Length, maxBytes + 1)];
            stream.ReadExactly(bytes);
            files.Add(new PickedFile(Path.GetFileName(path), bytes));
        }

        return files;
    }

    /// <summary>
    /// Asks the person for a folder.
    /// </summary>
    /// <param name="startIn">The folder the picker opens in, when it exists.</param>
    /// <returns>The folder picked, or nothing when the person cancelled.</returns>
    public string? PickFolder(string startIn)
    {
        var picker = new OpenFolderDialog { InitialDirectory = Directory.Exists(startIn) ? startIn : "" };
        return picker.ShowDialog(_window) == true ? picker.FolderName : null;
    }

    /// <summary>
    /// Opens the mail program of the person with a new message already written. The application
    /// sends nothing itself: the person does, from the mail program.
    /// </summary>
    public void OpenMail(string subject, string body) =>
        Process.Start(new ProcessStartInfo($"mailto:?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}") { UseShellExecute = true });

    /// <summary>
    /// The window, opened the first time it is needed. While a dialog is open it is not closed:
    /// the dialog is answered first, as a dialog window would ask. Once it is closed, the page
    /// shown is closed with it.
    /// </summary>
    private ShellWindow OpenWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        _window = new ShellWindow { DataContext = _shell };
        // The window opens where it was, at the size it had when not maximized: it never opens maximized, which the person does when they want it.
        if (_lastPlacement() is WindowPlacement placement && IsOnScreen(placement))
        {
            _window.WindowStartupLocation = WindowStartupLocation.Manual;
            _window.Left = placement.Left;
            _window.Top = placement.Top;
            _window.Width = Math.Max(placement.Width, _window.MinWidth);
            _window.Height = Math.Max(placement.Height, _window.MinHeight);
        }

        // Where the window is is kept while it is still there to say: once closed, it no longer says it was maximized.
        _window.Closing += (_, closing) =>
        {
            closing.Cancel = _shell.Dialogs.Count > 0;
            if (!closing.Cancel && PlacementOf(_window) is WindowPlacement placement)
            {
                _keepPlacement(placement);
            }
        };
        _window.Closed += (_, _) =>
        {
            Action? closed = _onPageClosed;
            _onPageClosed = null;
            closed?.Invoke();
        };
        _window.Show();
        return _window;
    }

    /// <summary>Whether enough of the top of the window would fall on the screens there are now, which may not be the ones it was closed on.</summary>
    private static bool IsOnScreen(WindowPlacement placement)
    {
        var screens = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        Rect top = Rect.Intersect(screens, new Rect(placement.Left, placement.Top, placement.Width, _minVisibleHeight));
        return !top.IsEmpty && top.Width >= _minVisibleWidth && top.Height >= _minVisibleHeight;
    }

    /// <summary>
    /// Where the window is, with the sizes it has when it is not maximized nor minimized; or nothing
    /// when Windows does not know them, as for a window that was never shown other than maximized.
    /// </summary>
    private static WindowPlacement? PlacementOf(Window window)
    {
        Rect normal = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
        bool known = !normal.IsEmpty && double.IsFinite(normal.Left) && double.IsFinite(normal.Top) && double.IsFinite(normal.Width) && double.IsFinite(normal.Height);
        return known ? new WindowPlacement(normal.Left, normal.Top, normal.Width, normal.Height, window.WindowState == WindowState.Maximized) : null;
    }

    /// <summary>A file the person picked.</summary>
    /// <param name="Name">The name of the file, without its folder.</param>
    /// <param name="Bytes">What it holds, up to one byte past the most the caller takes.</param>
    public sealed record PickedFile(string Name, byte[] Bytes);
}
