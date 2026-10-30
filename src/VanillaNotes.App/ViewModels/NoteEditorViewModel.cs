using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VanillaNotes.App.Converters;
using VanillaNotes.App.Services;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Validation;

namespace VanillaNotes.App.ViewModels;

/// <summary>
/// The open note: its title and its text, in the way of writing chosen, saved on their own a
/// moment after the person stops typing; its folder and its tags, saved at once; its attached
/// files; and deleting it.
/// A save based on an old version is refused, and the person is offered the latest one.
/// </summary>
public sealed partial class NoteEditorViewModel : ObservableObject
{
    /// <summary>How long the editor waits after the last key before it saves, as in the web version.</summary>
    private static readonly TimeSpan _saveDelay = TimeSpan.FromMilliseconds(800);

    private readonly NoteService _notes;

    private readonly FolderService _folders;

    private readonly AttachmentService _attachments;

    private readonly Action<long> _forgetImage;

    private readonly DialogService _dialogs;

    private readonly LanguageService _texts;

    private readonly FailureMessages _failures;

    private readonly PdfPrinter _printer;

    private readonly DispatcherTimer _saveTimer;

    /// <summary>The version of the note the title and the text on screen are based on.</summary>
    private int _version;

    /// <summary>Whether the title or the text changed since they were last saved.</summary>
    private bool _isDirty;

    /// <summary>Whether the fields are being filled from a note, which is not the person writing.</summary>
    private bool _isLoading;

    /// <summary>The way of writing the person chose, which every note opens in unless the visual way would damage it.</summary>
    private string _chosenMode;

    /// <summary>Whether the way of writing is being changed to keep a note from being damaged, not by the person.</summary>
    private bool _isGuarding;

    /// <summary>Whether a note is open.</summary>
    [ObservableProperty]
    private bool _hasNote;

    /// <summary>Whether the open note is pinned, and so first in the list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinText))]
    private bool _isPinned;

    /// <summary>When the open note was moved to the trash, or nothing when it is not there.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInTrash), nameof(IsWritable), nameof(TrashNotice))]
    private DateTimeOffset? _deletedAt;

    /// <summary>The title.</summary>
    [ObservableProperty]
    private string _title = "";

    /// <summary>The text, as Markdown.</summary>
    [ObservableProperty]
    private string _content = "";

    /// <summary>The way of writing: visual, Markdown, or read only.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisual), nameof(IsMarkdown), nameof(IsReading))]
    [NotifyCanExecuteChangedFor(nameof(InsertImageCommand))]
    private string _mode;

    /// <summary>Whether the text is shown as it is saved, or what keeps it from being saved.</summary>
    [ObservableProperty]
    private string _status = "";

    /// <summary>The folder of the note.</summary>
    [ObservableProperty]
    private FolderOption? _selectedFolder;

    /// <summary>The tag being typed, to add.</summary>
    [ObservableProperty]
    private string _newTag = "";

    /// <summary>Whether the last save was refused because the note changed somewhere else.</summary>
    [ObservableProperty]
    private bool _isConflict;

    /// <summary>What went wrong the last time.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailure))]
    private string _failure = "";

    /// <summary>Whether the visual way can write the open note without losing anything of it; told by the visual editor.</summary>
    [ObservableProperty]
    private bool _isVisualAllowed = true;

    /// <summary>Whether the spelling of what is written is checked.</summary>
    [ObservableProperty]
    private bool _spellcheck;

    /// <summary>An attached image to write where the cursor is, told to the editor of the way in use; nothing once it is written.</summary>
    [ObservableProperty]
    private AttachmentItemViewModel? _imageToInsert;

    /// <param name="imageOf">The picture of an attached image, by its number.</param>
    /// <param name="forgetImage">Forgets the picture of an attachment that was removed.</param>
    /// <param name="editorMode">The way of writing a note opens in, as the settings of the profile say.</param>
    /// <param name="spellcheck">Whether the spelling of what is written is checked, as the settings of the profile say.</param>
    public NoteEditorViewModel(
        NoteService notes,
        FolderService folders,
        AttachmentService attachments,
        Func<long, ImageSource?> imageOf,
        Action<long> forgetImage,
        DialogService dialogs,
        LanguageService texts,
        FailureMessages failures,
        PdfPrinter printer,
        string editorMode,
        bool spellcheck)
    {
        _printer = printer;
        ImageOf = imageOf;
        _attachments = attachments;
        _forgetImage = forgetImage;
        _spellcheck = spellcheck;
        AskLink = () => dialogs.Ask(texts["editor.linkPrompt"]);
        PasteImage = AttachPasted;
        _notes = notes;
        _folders = folders;
        _dialogs = dialogs;
        _texts = texts;
        _failures = failures;
        _mode = editorMode;
        _chosenMode = editorMode;
        _saveTimer = new DispatcherTimer { Interval = _saveDelay };
        _saveTimer.Tick += (_, _) => Save();
    }

    /// <summary>A number that changes each time the title is to take the keyboard.</summary>
    [ObservableProperty]
    private int _titleFocusRequest;

    /// <summary>A number that changes each time the text of the note is to take the keyboard.</summary>
    [ObservableProperty]
    private int _textFocusRequest;

    /// <summary>Told when the title or the text of the note was saved.</summary>
    public event Action? Saved;

    /// <summary>Told when the folder or the tags of the note changed.</summary>
    public event Action? Organized;

    /// <summary>Told when the note was deleted.</summary>
    public event Action? Deleted;

    /// <summary>Asks the person for the address of a link.</summary>
    public Func<string?> AskLink { get; }

    /// <summary>Attaches an image pasted, as the bytes of a PNG file, and writes it where the cursor is.</summary>
    public Action<byte[]> PasteImage { get; }

    /// <summary>The picture of an attached image, by its number.</summary>
    public Func<long, ImageSource?> ImageOf { get; }

    /// <summary>The files attached to the note.</summary>
    public ObservableCollection<AttachmentItemViewModel> Attachments { get; } = [];

    /// <summary>The number of the open note.</summary>
    public long NoteId { get; private set; }

    /// <summary>The ways of writing, each with its name.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Modes =>
        [new("visual", _texts["editor.mode.visual"]), new("markdown", _texts["editor.mode.markdown"]), new("read", _texts["editor.mode.read"])];

    /// <summary>The folders a note can be in, the lack of one first.</summary>
    public ObservableCollection<FolderOption> FolderOptions { get; } = [];

    /// <summary>The tags of the note.</summary>
    public ObservableCollection<string> Tags { get; } = [];

    /// <summary>Whether the note is written in the visual way.</summary>
    public bool IsVisual => Mode == "visual";

    /// <summary>Whether the note is written as Markdown, with its preview.</summary>
    public bool IsMarkdown => Mode == "markdown";

    /// <summary>Whether the note is only read.</summary>
    public bool IsReading => Mode == "read";

    /// <summary>Whether something went wrong.</summary>
    public bool HasFailure => Failure.Length > 0;

    /// <summary>What the button that pins the note says: to pin it, or to unpin it.</summary>
    public string PinText => _texts[IsPinned ? "editor.unpin" : "editor.pin"];

    /// <summary>Whether the open note is in the trash, where it is only read, put back, or deleted for good.</summary>
    public bool IsInTrash => DeletedAt is not null;

    /// <summary>Whether the open note can be written, filed, tagged, and given files: it is not in the trash.</summary>
    public bool IsWritable => !IsInTrash;

    /// <summary>What the open note in the trash says about itself: the day it will be deleted for good.</summary>
    public string TrashNotice => DeletedAt is DateTimeOffset deleted
        ? _texts.Format("editor.trash.notice", ("date", DateTextConverter.Describe(deleted + NoteService.TrashKeeping, "exact", _texts)))
        : "";

    private bool IsWriting => !IsReading;

    /// <summary>
    /// Opens a note, once what was typed in the one open is saved.
    /// </summary>
    /// <returns>Whether the note was opened; it is not when what was typed could not be saved and the person keeps it.</returns>
    public bool Open(long noteId)
    {
        if (!Leave())
        {
            return false;
        }

        try
        {
            Show(_notes.Get(noteId));
            return true;
        }
        catch (AppException failure)
        {
            Close();
            Failure = _failures.Text(failure);
            return false;
        }
    }

    /// <summary>
    /// Saves what was typed before the note is left. When it cannot be saved, the person is asked
    /// whether to leave it.
    /// </summary>
    /// <returns>Whether the note can be left.</returns>
    public bool Leave()
    {
        if (!HasNote || !_isDirty)
        {
            return true;
        }

        Save();
        return !_isDirty || _dialogs.Confirm(_texts["workspace.discardConfirm"], "dialog.confirm", isDanger: true);
    }

    /// <summary>
    /// Follows the settings of the profile once they changed: the next note opens in the way of
    /// writing chosen, the spelling is checked or not at once, and the texts are in the language
    /// of the interface.
    /// </summary>
    public void UsePreferences(string editorMode, bool spellcheck)
    {
        _chosenMode = editorMode;
        Spellcheck = spellcheck;
        OnPropertyChanged(nameof(Modes));
        RefreshFolders();
        if (HasNote)
        {
            RefreshAttachments();
        }
    }

    /// <summary>
    /// Saves what was typed and shows no note, asking nothing: what is closed while the person is
    /// away cannot wait for an answer. Text that could not be saved is lost.
    /// </summary>
    public void SaveAndClose()
    {
        if (HasNote)
        {
            Save();
        }

        Close();
    }

    /// <summary>Shows no note.</summary>
    public void Close()
    {
        _saveTimer.Stop();
        _isLoading = true;
        HasNote = false;
        DeletedAt = null;
        NoteId = 0;
        Title = "";
        Content = "";
        Tags.Clear();
        Attachments.Clear();
        _isDirty = false;
        _isLoading = false;
    }

    /// <summary>Shows the folder the open note was moved to from somewhere else, as the sidebar does when a note is dropped onto one.</summary>
    public void ShowFolder(long? folderId)
    {
        _isLoading = true;
        SelectedFolder = FolderOptions.FirstOrDefault(option => option.Id == folderId) ?? FolderOptions[0];
        _isLoading = false;
    }

    /// <summary>Reads the folders a note can be in again, after they changed in the sidebar.</summary>
    public void RefreshFolders()
    {
        _isLoading = true;
        long? current = SelectedFolder?.Id;
        FolderOptions.Clear();
        FolderOptions.Add(new FolderOption(null, _texts["editor.noFolder"]));
        foreach (Folder folder in _folders.List())
        {
            FolderOptions.Add(new FolderOption(folder.Id, folder.Name));
        }

        SelectedFolder = FolderOptions.FirstOrDefault(option => option.Id == current) ?? FolderOptions[0];
        _isLoading = false;
    }

    // -- Writing -- //

    partial void OnIsVisualAllowedChanged(bool value) => KeepVisualSafe();

    partial void OnModeChanged(string value)
    {
        if (!_isGuarding)
        {
            _chosenMode = value;
        }

        KeepVisualSafe();
    }

    /// <summary>A note the visual way would damage is shown as Markdown, and the person is told why.</summary>
    private void KeepVisualSafe()
    {
        if (!IsVisualAllowed && IsVisual && HasNote)
        {
            _isGuarding = true;
            Mode = "markdown";
            _isGuarding = false;
            Failure = _texts["editor.visualFailure"];
        }
    }

    /// <summary>Goes to the next way of writing, round to the first after the last; the visual way is passed over when it would damage the note.</summary>
    [RelayCommand]
    private void NextMode()
    {
        if (!HasNote || IsInTrash)
        {
            return;
        }

        List<string> modes = Modes.Select(mode => mode.Key).Where(mode => mode != "visual" || IsVisualAllowed).ToList();
        Mode = modes[(modes.IndexOf(Mode) + 1) % modes.Count];
    }

    partial void OnTitleChanged(string value) => Typed();

    partial void OnContentChanged(string value) => Typed();

    private void Typed()
    {
        if (_isLoading || !HasNote)
        {
            return;
        }

        _isDirty = true;
        Status = _texts["editor.status.unsaved"];
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>
    /// Saves the title and the text, when they changed and the note did not change somewhere else.
    /// It runs on its own a moment after the person stops typing, and at once when they ask.
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        _saveTimer.Stop();
        if (!_isDirty || IsConflict)
        {
            return;
        }

        Failure = "";
        Status = _texts["editor.status.saving"];
        try
        {
            Note saved = _notes.Edit(NoteId, _version, Title, Content);
            _version = saved.Version;
            _isDirty = false;
            Status = _texts["editor.status.saved"];
            Saved?.Invoke();
        }
        catch (ConflictException failure)
        {
            IsConflict = true;
            Status = _texts["editor.status.notSaved"];
            Failure = _failures.Text(failure);
        }
        catch (AppException failure)
        {
            Status = _texts["editor.status.notSaved"];
            Failure = _failures.Text(failure);
        }
    }

    /// <summary>Puts the latest version of the note in place of what was typed and could not be saved.</summary>
    [RelayCommand]
    private void LoadLatest()
    {
        _isDirty = false;
        Show(_notes.Get(NoteId));
    }

    // -- Organizing -- //

    partial void OnSelectedFolderChanged(FolderOption? value)
    {
        if (_isLoading || !HasNote || value is null)
        {
            return;
        }

        Attempt(() =>
        {
            _notes.Move(NoteId, value.Id);
            Organized?.Invoke();
        });
    }

    /// <summary>Pins the note, so that it comes first in the list, or unpins it.</summary>
    [RelayCommand]
    private void TogglePin() => Attempt(() =>
    {
        IsPinned = _notes.Pin(NoteId, !IsPinned).IsPinned;
        Organized?.Invoke();
    });

    /// <summary>Puts the tag typed on the note.</summary>
    [RelayCommand]
    private void AddTag()
    {
        string tag = NewTag.Trim();
        if (tag.Length == 0)
        {
            return;
        }

        SetTags([.. Tags, tag]);
        if (!HasFailure)
        {
            NewTag = "";
        }
    }

    /// <summary>Takes a tag off the note.</summary>
    [RelayCommand]
    private void RemoveTag(string tag) => SetTags(Tags.Where(name => name != tag).ToList());

    private void SetTags(IReadOnlyList<string> tags) => Attempt(() =>
    {
        Note tagged = _notes.SetTags(NoteId, tags);
        Tags.Clear();
        foreach (string tag in tagged.Tags)
        {
            Tags.Add(tag);
        }

        Organized?.Invoke();
    });

    // -- Attachments -- //

    /// <summary>Attaches a file the person picks to the note.</summary>
    [RelayCommand]
    private void Attach()
    {
        DialogService.PickedFile? file = _dialogs.PickFile(_texts["editor.attach.kinds"], AttachmentValidator.MimeTypesByExtension.Keys, AttachmentValidator.MaxSize);
        if (file is null)
        {
            return;
        }

        Attempt(() =>
        {
            _attachments.Add(NoteId, file.Name, AttachmentValidator.MimeTypeOf(file.Name), file.Bytes);
            RefreshAttachments();
        });
    }

    /// <summary>
    /// Attaches the files dragged onto the note, and writes the images among them where the cursor
    /// is. A file that is not accepted is left out, and the first of them says why.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTakeFiles))]
    private void AttachFiles(string[] paths)
    {
        Failure = "";
        string? refused = null;
        var images = new List<long>();
        foreach (DialogService.PickedFile file in _dialogs.ReadFiles(paths, AttachmentValidator.MaxSize))
        {
            try
            {
                Attachment added = _attachments.Add(NoteId, file.Name, AttachmentValidator.MimeTypeOf(file.Name), file.Bytes);
                if (added.MimeType.StartsWith("image/", StringComparison.Ordinal))
                {
                    images.Add(added.Id);
                }
            }
            catch (AppException failure)
            {
                refused ??= _failures.Text(failure);
            }
        }

        RefreshAttachments();
        foreach (long id in images)
        {
            InsertImage(Attachments.First(attachment => attachment.Id == id));
        }

        Failure = refused ?? "";
    }

    /// <summary>Whether files can be attached now: a note is open, out of the trash, in a way of writing.</summary>
    private bool CanTakeFiles() => HasNote && IsWritable && IsWriting;

    /// <summary>An image pasted is attached under the moment it was pasted, and written where the cursor is.</summary>
    private void AttachPasted(byte[] png)
    {
        if (!CanTakeFiles())
        {
            return;
        }

        Attempt(() =>
        {
            Attachment added = _attachments.Add(NoteId, $"pasted-{DateTime.Now:yyyyMMdd-HHmmss}.png", "image/png", png);
            RefreshAttachments();
            InsertImage(Attachments.First(attachment => attachment.Id == added.Id));
        });
    }

    /// <summary>Writes an attached image into the text, where the cursor is; a note that is only read is not written in.</summary>
    [RelayCommand(CanExecute = nameof(IsWriting))]
    private void InsertImage(AttachmentItemViewModel attachment)
    {
        ImageToInsert = attachment;
        ImageToInsert = null;
    }

    /// <summary>Saves a copy of an attached file where the person picks.</summary>
    [RelayCommand]
    private void SaveAttachment(AttachmentItemViewModel attachment) => Attempt(() => _dialogs.SaveFile(attachment.FileName, _attachments.ReadContent(attachment.Id)));

    /// <summary>Removes an attached file from the note, once the person agrees.</summary>
    [RelayCommand]
    private void RemoveAttachment(AttachmentItemViewModel attachment)
    {
        if (!_dialogs.Confirm(_texts.Format("editor.attachment.deleteConfirm", ("filename", attachment.FileName)), "editor.attachment.delete", isDanger: true))
        {
            return;
        }

        Attempt(() =>
        {
            _attachments.Remove(attachment.Id);
            _forgetImage(attachment.Id);
            RefreshAttachments();
        });
    }

    private void RefreshAttachments()
    {
        Attachments.Clear();
        foreach (Attachment attachment in _attachments.List(NoteId))
        {
            Attachments.Add(new AttachmentItemViewModel(attachment, _texts));
        }
    }

    // -- Printing and deleting -- //

    /// <summary>Prints the note, to paper or to a PDF file, once what was typed is saved.</summary>
    [RelayCommand]
    private void Print()
    {
        if (!HasNote)
        {
            return;
        }

        Save();
        _printer.Print(Title, Content, ImageOf);
    }

    /// <summary>Takes the keyboard to the title, with it selected, so that a new note can be named by typing.</summary>
    public void FocusTitle() => TitleFocusRequest++;

    /// <summary>Takes the keyboard from the title to the text of the note.</summary>
    [RelayCommand]
    private void GoToText() => TextFocusRequest++;

    /// <summary>Moves the note to the trash, once what was typed is saved; it can be put back from there.</summary>
    [RelayCommand]
    private void Delete()
    {
        if (!Leave())
        {
            return;
        }

        Attempt(() =>
        {
            _notes.Trash(NoteId);
            Close();
            Deleted?.Invoke();
        });
    }

    /// <summary>Puts the note in the trash back as it was, and leaves it open.</summary>
    [RelayCommand]
    private void Restore() => Attempt(() =>
    {
        Show(_notes.Restore(NoteId));
        Organized?.Invoke();
    });

    /// <summary>Deletes the note in the trash for good, with its attachments, once the person agrees.</summary>
    [RelayCommand]
    private void DeleteForever()
    {
        if (!_dialogs.Confirm(_texts.Format("editor.deleteConfirm", ("title", Title)), "editor.deleteForever", isDanger: true))
        {
            return;
        }

        Attempt(() =>
        {
            _notes.Remove(NoteId);
            Close();
            Deleted?.Invoke();
        });
    }

    // -- Helpers -- //

    private void Show(Note note)
    {
        _saveTimer.Stop();
        IsConflict = false;
        Failure = "";
        IsVisualAllowed = true;
        DeletedAt = note.DeletedAt;
        IsPinned = note.IsPinned;
        _isGuarding = true;
        Mode = IsInTrash ? "read" : _chosenMode;
        _isGuarding = false;

        _isLoading = true;
        NoteId = note.Id;
        _version = note.Version;
        HasNote = true;
        Title = note.Title;

        // The visual editor reads the text as it arrives, and says whether it can write it.
        Content = note.Content;
        Tags.Clear();
        foreach (string tag in note.Tags)
        {
            Tags.Add(tag);
        }

        _isDirty = false;
        Status = _texts["editor.status.saved"];
        _isLoading = false;
        RefreshAttachments();
        RefreshFolders();
        _isLoading = true;
        SelectedFolder = FolderOptions.FirstOrDefault(option => option.Id == note.FolderId) ?? FolderOptions[0];
        _isLoading = false;
    }

    private void Attempt(Action action)
    {
        Failure = "";
        try
        {
            action();
        }
        catch (AppException failure)
        {
            Failure = _failures.Text(failure);
        }
    }

    /// <summary>A folder a note can be put in, or the lack of one.</summary>
    /// <param name="Id">The number of the folder, or nothing for no folder.</param>
    /// <param name="Name">What the choice shows.</param>
    public sealed record FolderOption(long? Id, string Name);
}
