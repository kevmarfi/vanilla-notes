using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VanillaNotes.App.Services;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;

namespace VanillaNotes.App.ViewModels;

/// <summary>
/// The workspace of an open profile: the sidebar, the list of notes, and the open note, kept in
/// step. What belongs to no single area is here: a new note, which starts in the folder or with
/// the tag being looked at; the lists brought up to date after a note is saved; the settings; and
/// closing the profile, on its own or to restore a backup, which needs every profile closed; and
/// emptying the trash. A profile opens on the note and the folder it was left on, once the notes
/// that have been in its trash too long are deleted for good. A private profile is closed after
/// the time with nothing done its settings chose, and when Windows is locked.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ProfileSession _session;

    private readonly NoteService _notes;

    private readonly WorkspaceService _workspace;

    private readonly IdleWatcher _idle;

    /// <summary>The minutes with nothing done that close the profile when it has a password, as its settings say now.</summary>
    private string _lockAfter;

    private readonly BackupService _backups;

    private readonly DialogService _dialogs;

    private readonly LanguageService _texts;

    private readonly FailureMessages _failures;

    private readonly Func<ProfileChooserViewModel> _chooser;

    private readonly Func<SettingsViewModel> _settings;

    /// <summary>Whether the profile was already closed, so that it is closed once.</summary>
    private bool _isLeft;

    /// <summary>Whether a backup is being restored, while the window takes nothing from the person.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isRestoring;

    public MainViewModel(
        ProfileSession session,
        SidebarViewModel sidebar,
        NoteListViewModel noteList,
        NoteEditorViewModel editor,
        NoteService notes,
        WorkspaceService workspace,
        IdleWatcher idle,
        string lockAfter,
        BackupService backups,
        DialogService dialogs,
        LanguageService texts,
        FailureMessages failures,
        Func<ProfileChooserViewModel> chooser,
        Func<SettingsViewModel> settings)
    {
        _session = session;
        Sidebar = sidebar;
        NoteList = noteList;
        Editor = editor;
        _notes = notes;
        _workspace = workspace;
        _idle = idle;
        _lockAfter = lockAfter;
        _backups = backups;
        _dialogs = dialogs;
        _texts = texts;
        _failures = failures;
        _chooser = chooser;
        _settings = settings;

        RemoveExpired();
        WorkspacePlace place = _workspace.Get();
        Sidebar.ChooseAgain(place.FolderId, place.WithoutFolder);
        Sidebar.FilterChanged += () => _ = ReloadList();
        NoteList.NoteChosen += Open;
        NoteList.NewNoteRequested += CreateNote;
        NoteList.EmptyTrashRequested += EmptyTrash;
        Sidebar.NoteDropped += MoveNote;
        Editor.Saved += () => _ = ReloadList();
        Editor.Organized += Refresh;
        Editor.Deleted += Refresh;
        Editor.RefreshFolders();
        if (place.NoteId is long noteId)
        {
            Editor.Open(noteId);
        }

        _ = ReloadList();
        WatchIdle();
    }

    /// <summary>The name of the open profile.</summary>
    public string ProfileName => _session.Profile.Name;

    /// <summary>The sidebar.</summary>
    public SidebarViewModel Sidebar { get; }

    /// <summary>The list of notes.</summary>
    public NoteListViewModel NoteList { get; }

    /// <summary>The open note.</summary>
    public NoteEditorViewModel Editor { get; }

    /// <summary>Whether the window takes what the person does: always, but while a backup is restored.</summary>
    public bool IsIdle => !IsRestoring;

    /// <summary>
    /// Closes the profile once its page is replaced or the window is closed: what was typed is saved,
    /// where the person was is kept for the next time, and its key is forgotten.
    /// </summary>
    public void Leave()
    {
        if (_isLeft)
        {
            return;
        }

        _isLeft = true;
        _idle.Stop();
        Editor.Leave();
        try
        {
            _workspace.Remember(new WorkspacePlace(Editor.HasNote ? Editor.NoteId : null, Sidebar.SelectedFolder?.Id, Sidebar.IsNoFolderSelected));
        }
        finally
        {
            _session.Dispose();
        }
    }

    /// <summary>Closes the profile and goes back to the window of profiles.</summary>
    [RelayCommand]
    private void CloseProfile()
    {
        if (!Editor.Leave())
        {
            return;
        }

        _dialogs.Show(_chooser());
    }

    /// <summary>Opens the settings of the profile; what they change is followed at once.</summary>
    [RelayCommand]
    private void OpenSettings()
    {
        SettingsViewModel settings = _settings();
        settings.Changed += Follow;
        settings.RestoreRequested += name => _ = RestoreAsync(name);
        settings.Renamed += () => OnPropertyChanged(nameof(ProfileName));
        settings.Imported += Refresh;
        settings.Deleted += LeaveDeleted;
        _dialogs.ShowDialog(settings);

        // A password set in the settings makes the profile one to close when nothing is done.
        if (!_isLeft)
        {
            WatchIdle();
        }
    }

    /// <summary>
    /// Closes the profile and goes back to the window of profiles, with nothing asked: what was
    /// typed is saved, and every dialog open is closed as if cancelled.
    /// </summary>
    private void Lock()
    {
        if (_isLeft)
        {
            return;
        }

        Editor.SaveAndClose();
        _dialogs.CloseAllAndShow(_chooser());
    }

    /// <summary>Watches for the time with nothing done when the profile has a password, which is what there is to lock.</summary>
    private void WatchIdle()
    {
        if (_session.Profile.HasPassword)
        {
            _idle.Watch(_lockAfter, Lock);
        }
    }

    /// <summary>Goes back to the window of profiles once the profile was deleted; it was closed with it.</summary>
    private void LeaveDeleted()
    {
        _isLeft = true;
        _dialogs.Show(_chooser());
        _dialogs.Notify(_texts["settings.delete.done"]);
    }

    /// <summary>
    /// Closes the profile and puts a backup in place of all the data, off the thread of the
    /// window; then goes back to the window of profiles, which lists the profiles of the backup.
    /// </summary>
    private async Task RestoreAsync(string name)
    {
        if (!Editor.Leave())
        {
            return;
        }

        IsRestoring = true;
        Leave();
        string outcome = _texts["settings.restore.done"];
        try
        {
            await Task.Run(() => _backups.Restore(name));
        }
        catch (AppException failure)
        {
            outcome = _failures.Text(failure);
        }

        _dialogs.Show(_chooser());
        _dialogs.Notify(outcome);
    }

    private void Follow(ProfileSettings chosen)
    {
        _lockAfter = chosen.LockAfter;
        WatchIdle();
        Editor.UsePreferences(chosen.EditorMode, chosen.Spellcheck);
        _ = ReloadList();
    }

    private void Open(long noteId)
    {
        if (!Editor.Open(noteId))
        {
            NoteList.Choose(Editor.HasNote ? Editor.NoteId : null);
        }
    }

    private void CreateNote()
    {
        if (!Editor.Leave())
        {
            return;
        }

        // A note is not made in the trash: it is made in the whole list.
        if (Sidebar.IsTrashSelected)
        {
            Sidebar.ChooseAllCommand.Execute(null);
        }

        NoteQuery looking = Sidebar.Query;
        try
        {
            Note note = _notes.Create(
                _texts["workspace.untitledNote"],
                "",
                looking.FolderId,
                looking.Tag is null ? null : [looking.Tag]);
            Sidebar.Refresh();
            Editor.Open(note.Id);
            Editor.FocusTitle();
            _ = ReloadList();
        }
        catch (AppException failure)
        {
            _dialogs.Notify(_failures.Text(failure));
        }
    }

    /// <summary>
    /// Moves a note dropped onto a folder of the sidebar, or onto the notes in no folder. A note in
    /// the trash is put back, not moved, so nothing happens while the trash is shown.
    /// </summary>
    private void MoveNote(long noteId, long? folderId)
    {
        if (Sidebar.IsTrashSelected)
        {
            return;
        }

        try
        {
            _notes.Move(noteId, folderId);
            if (Editor.HasNote && Editor.NoteId == noteId)
            {
                Editor.ShowFolder(folderId);
            }

            Refresh();
        }
        catch (AppException failure)
        {
            _dialogs.Notify(_failures.Text(failure));
        }
    }

    /// <summary>Deletes for good every note in the trash, once the person agrees.</summary>
    private void EmptyTrash()
    {
        if (!_dialogs.Confirm(_texts["list.emptyTrashConfirm"], "list.emptyTrash", isDanger: true))
        {
            return;
        }

        try
        {
            _notes.EmptyTrash();
            if (Editor.IsInTrash)
            {
                Editor.Close();
            }

            Refresh();
        }
        catch (AppException failure)
        {
            _dialogs.Notify(_failures.Text(failure));
        }
    }

    /// <summary>Deletes for good the notes that have been in the trash longer than it keeps them; a failure is told and the profile opens anyway.</summary>
    private void RemoveExpired()
    {
        try
        {
            _notes.RemoveExpired();
        }
        catch (AppException failure)
        {
            _dialogs.Notify(_failures.Text(failure));
        }
    }

    private void Refresh()
    {
        Sidebar.Refresh();
        Editor.RefreshFolders();
        _ = ReloadList();
    }

    private Task ReloadList() => NoteList.LoadAsync(Sidebar.Query, Sidebar.Heading, Editor.HasNote ? Editor.NoteId : null);
}
