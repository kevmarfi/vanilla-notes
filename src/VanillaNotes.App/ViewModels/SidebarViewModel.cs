using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VanillaNotes.App.Services;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;

namespace VanillaNotes.App.ViewModels;

/// <summary>
/// The sidebar: the search, the whole list, the notes in no folder, or the trash, the folders with
/// how many notes each holds, and the tags in use. It says which notes the person asks to see; making,
/// renaming, and deleting folders happens here too.
/// </summary>
public sealed partial class SidebarViewModel : ObservableObject
{
    /// <summary>How long the search waits after the last key before it asks, so that a word typed is one search.</summary>
    private static readonly TimeSpan _searchDelay = TimeSpan.FromMilliseconds(300);

    private readonly FolderService _folders;

    private readonly TagService _tags;

    private readonly DialogService _dialogs;

    private readonly LanguageService _texts;

    private readonly FailureMessages _failures;

    private readonly DispatcherTimer _searchTimer;

    /// <summary>The text of the search.</summary>
    [ObservableProperty]
    private string _searchText = "";

    /// <summary>Whether the whole list is chosen.</summary>
    [ObservableProperty]
    private bool _isAllSelected = true;

    /// <summary>Whether the notes in no folder are chosen.</summary>
    [ObservableProperty]
    private bool _isNoFolderSelected;

    /// <summary>Whether the trash is chosen.</summary>
    [ObservableProperty]
    private bool _isTrashSelected;

    /// <summary>The folder chosen, or nothing.</summary>
    [ObservableProperty]
    private FolderItemViewModel? _selectedFolder;

    /// <summary>The tag chosen, or nothing.</summary>
    [ObservableProperty]
    private TagItemViewModel? _selectedTag;

    /// <summary>Whether the box to name a new folder is open.</summary>
    [ObservableProperty]
    private bool _isAddingFolder;

    /// <summary>The name of the new folder.</summary>
    [ObservableProperty]
    private string _newFolderName = "";

    /// <summary>What went wrong the last time a folder was made, renamed, or deleted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailure))]
    private string _failure = "";

    /// <summary>Whether the choice is being changed by the sidebar itself, so that it is told once.</summary>
    private bool _isChoosing;

    public SidebarViewModel(FolderService folders, TagService tags, DialogService dialogs, LanguageService texts, FailureMessages failures)
    {
        _folders = folders;
        _tags = tags;
        _dialogs = dialogs;
        _texts = texts;
        _failures = failures;
        _searchTimer = new DispatcherTimer { Interval = _searchDelay };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            FilterChanged?.Invoke();
        };
        Refresh();
    }

    /// <summary>Told when the notes the person asks to see change.</summary>
    public event Action? FilterChanged;

    /// <summary>Told with a note dropped onto a folder, or onto the notes in no folder, and the number of that folder.</summary>
    public event Action<long, long?>? NoteDropped;

    /// <summary>The folders, by name.</summary>
    public ObservableCollection<FolderItemViewModel> Folders { get; } = [];

    /// <summary>The tags in use, by name.</summary>
    public ObservableCollection<TagItemViewModel> Tags { get; } = [];

    /// <summary>Whether something went wrong.</summary>
    public bool HasFailure => Failure.Length > 0;

    /// <summary>Whether a search is being made.</summary>
    public bool IsSearching => SearchText.Trim().Length > 0;

    /// <summary>
    /// What the list is asked for: the search, if there is one, within the folder, the tag, or the trash chosen.
    /// </summary>
    public NoteQuery Query => new(
        FolderId: SelectedFolder?.Id,
        WithoutFolder: IsNoFolderSelected,
        Tag: SelectedTag?.Name,
        Text: IsSearching ? SearchText : null,
        InTrash: IsTrashSelected);

    /// <summary>The heading of the list for what is chosen.</summary>
    public string Heading =>
        IsSearching ? _texts["sidebar.searchResults"]
        : SelectedFolder is not null ? SelectedFolder.Name
        : SelectedTag is not null ? $"#{SelectedTag.Name}"
        : IsNoFolderSelected ? _texts["sidebar.noFolder"]
        : IsTrashSelected ? _texts["sidebar.trash"]
        : _texts["sidebar.allNotes"];

    /// <summary>
    /// Reads the folders and the tags again, with their counts, and keeps what was chosen when it
    /// is still there.
    /// </summary>
    public void Refresh()
    {
        long? folderId = SelectedFolder?.Id;
        string? tag = SelectedTag?.Name;
        _isChoosing = true;
        Folders.Clear();
        foreach (Folder folder in _folders.List())
        {
            Folders.Add(new FolderItemViewModel(folder));
        }

        Tags.Clear();
        foreach (Tag used in _tags.List())
        {
            Tags.Add(new TagItemViewModel(used));
        }

        SelectedFolder = Folders.FirstOrDefault(folder => folder.Id == folderId);
        SelectedTag = Tags.FirstOrDefault(item => item.Name == tag);
        bool lost = (folderId is not null && SelectedFolder is null) || (tag is not null && SelectedTag is null);
        _isChoosing = false;
        if (lost)
        {
            ChooseAll();
        }
    }

    // -- Choosing -- //

    /// <summary>
    /// Chooses again where the person was: a folder, or the notes in no folder. A folder that is no
    /// longer there leaves the whole list chosen.
    /// </summary>
    public void ChooseAgain(long? folderId, bool withoutFolder)
    {
        if (withoutFolder)
        {
            ChooseNoFolder();
        }
        else if (Folders.FirstOrDefault(folder => folder.Id == folderId) is FolderItemViewModel folder)
        {
            SelectedFolder = folder;
        }
    }

    /// <summary>Chooses the whole list.</summary>
    [RelayCommand]
    private void ChooseAll() => Choose(() => IsAllSelected = true);

    /// <summary>Chooses the notes in no folder.</summary>
    [RelayCommand]
    private void ChooseNoFolder() => Choose(() => IsNoFolderSelected = true);

    /// <summary>Takes a note dropped onto a folder, or onto the notes in no folder, for the workspace to move it.</summary>
    [RelayCommand]
    private void DropNote(DroppedNote drop) => NoteDropped?.Invoke(drop.NoteId, (drop.Target as FolderItemViewModel)?.Id);

    /// <summary>Chooses the trash.</summary>
    [RelayCommand]
    private void ChooseTrash() => Choose(() => IsTrashSelected = true);

    partial void OnSelectedFolderChanged(FolderItemViewModel? value)
    {
        if (value is not null && !_isChoosing)
        {
            Choose(() => SelectedFolder = value);
        }
    }

    partial void OnSelectedTagChanged(TagItemViewModel? value)
    {
        if (value is not null && !_isChoosing)
        {
            Choose(() => SelectedTag = value);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(Heading));
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    /// <summary>Empties the search.</summary>
    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    /// <summary>Clears every choice, makes one, and tells it once.</summary>
    private void Choose(Action choice)
    {
        _isChoosing = true;
        IsAllSelected = false;
        IsNoFolderSelected = false;
        IsTrashSelected = false;
        SelectedFolder = null;
        SelectedTag = null;
        choice();
        _isChoosing = false;
        OnPropertyChanged(nameof(Heading));
        FilterChanged?.Invoke();
    }

    // -- Folders -- //

    /// <summary>Opens or closes the box to name a new folder.</summary>
    [RelayCommand]
    private void ToggleAddFolder()
    {
        Failure = "";
        NewFolderName = "";
        IsAddingFolder = !IsAddingFolder;
    }

    /// <summary>Makes a folder with the name typed.</summary>
    [RelayCommand]
    private void AddFolder() => Attempt(() =>
    {
        _folders.Create(NewFolderName);
        IsAddingFolder = false;
        NewFolderName = "";
        Refresh();
    });

    /// <summary>Opens the name of a folder to be changed in its row.</summary>
    [RelayCommand]
    private void StartRename(FolderItemViewModel folder)
    {
        Failure = "";
        folder.NewName = folder.Name;
        folder.IsRenaming = true;
    }

    /// <summary>Gives a folder the name typed in its row.</summary>
    [RelayCommand]
    private void Rename(FolderItemViewModel folder) => Attempt(() =>
    {
        _folders.Rename(folder.Id, folder.NewName);
        folder.IsRenaming = false;
        Refresh();
        FilterChanged?.Invoke();
    });

    /// <summary>Leaves the name of a folder as it was.</summary>
    [RelayCommand]
    private void CancelRename(FolderItemViewModel folder) => folder.IsRenaming = false;

    /// <summary>Deletes a folder, once the person agrees; its notes are kept, in no folder.</summary>
    [RelayCommand]
    private void Delete(FolderItemViewModel folder)
    {
        if (!_dialogs.Confirm(_texts.Format("sidebar.deleteFolderConfirm", ("name", folder.Name)), "sidebar.deleteFolder", isDanger: true))
        {
            return;
        }

        Attempt(() =>
        {
            _folders.Remove(folder.Id);
            Refresh();
            FilterChanged?.Invoke();
        });
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
}
