using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VanillaNotes.App.Services;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;

namespace VanillaNotes.App.ViewModels;

/// <summary>
/// The notes asked for, by pages of 50, each with its tags and, when searching, a fragment of its
/// text; or the notes in the trash, which can be emptied, and where no note is made. The list is
/// read off the thread of the window, and an answer that arrives after a newer request was made is dropped.
/// </summary>
public sealed partial class NoteListViewModel : ObservableObject
{
    /// <summary>How many notes a page of the list holds, as in the web version.</summary>
    private const int _pageSize = 50;

    private readonly NoteService _notes;

    private readonly LanguageService _texts;

    private readonly FailureMessages _failures;

    private readonly Func<string> _dateStyle;

    private readonly Func<string> _noteOrder;

    /// <summary>The query of the list on screen.</summary>
    private NoteQuery _query = new();

    /// <summary>The number of the last request, so that only its answer is shown.</summary>
    private int _request;

    /// <summary>The heading of the list.</summary>
    [ObservableProperty]
    private string _heading = "";

    /// <summary>How many notes there are in all, said in words.</summary>
    [ObservableProperty]
    private string _countText = "";

    /// <summary>Whether there are notes beyond the ones shown.</summary>
    [ObservableProperty]
    private bool _hasMore;

    /// <summary>Whether there is no note to show.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEmptyTrash))]
    private bool _isEmpty;

    /// <summary>What the empty list says.</summary>
    [ObservableProperty]
    private string _emptyText = "";

    /// <summary>Whether the list is the trash.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEmptyTrash))]
    private bool _isTrash;

    /// <summary>The note chosen in the list.</summary>
    [ObservableProperty]
    private NoteItemViewModel? _selectedItem;

    /// <summary>What went wrong the last time the list was read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailure))]
    private string _failure = "";

    /// <summary>Whether the selection is being set by the list itself, so that the note is not opened again.</summary>
    private bool _isRestoring;

    /// <param name="dateStyle">How the dates of the list are written, as the settings of the profile say now.</param>
    /// <param name="noteOrder">The order of the list, as the settings of the profile say now.</param>
    public NoteListViewModel(NoteService notes, LanguageService texts, FailureMessages failures, Func<string> dateStyle, Func<string> noteOrder)
    {
        _noteOrder = noteOrder;
        _notes = notes;
        _texts = texts;
        _failures = failures;
        _dateStyle = dateStyle;
    }

    /// <summary>Told when the person picks a note.</summary>
    public event Action<long>? NoteChosen;

    /// <summary>Told when the person asks for a new note.</summary>
    public event Action? NewNoteRequested;

    /// <summary>Told when the person asks to empty the trash.</summary>
    public event Action? EmptyTrashRequested;

    /// <summary>Whether the trash is shown and holds a note to delete.</summary>
    public bool CanEmptyTrash => IsTrash && !IsEmpty;

    /// <summary>The notes shown.</summary>
    public ObservableCollection<NoteItemViewModel> Items { get; } = [];

    /// <summary>Whether something went wrong.</summary>
    public bool HasFailure => Failure.Length > 0;

    /// <summary>
    /// Reads the first page of a query, and keeps chosen the note that is open when it is in it.
    /// </summary>
    public async Task LoadAsync(NoteQuery query, string heading, long? openNoteId)
    {
        _query = query with { Limit = _pageSize, Offset = 0, Order = _noteOrder() };
        Heading = heading;
        IsTrash = query.InTrash;
        int request = ++_request;
        (IReadOnlyList<NoteItemViewModel> Items, int Total)? page = await Read(_query);
        if (request != _request || page is null)
        {
            return;
        }

        Items.Clear();
        Add(page.Value.Items);
        Show(page.Value.Total);
        Choose(openNoteId);
    }

    /// <summary>Chooses in the list the note that is open, without opening it again.</summary>
    public void Choose(long? noteId)
    {
        _isRestoring = true;
        SelectedItem = Items.FirstOrDefault(item => item.Id == noteId);
        _isRestoring = false;
    }

    /// <summary>Reads the next page of the list, after the notes shown.</summary>
    [RelayCommand]
    private async Task LoadMore()
    {
        NoteQuery next = _query with { Offset = Items.Count };
        int request = ++_request;
        (IReadOnlyList<NoteItemViewModel> Items, int Total)? page = await Read(next);
        if (request != _request || page is null)
        {
            return;
        }

        Add(page.Value.Items);
        Show(page.Value.Total);
    }

    /// <summary>Asks for a new note.</summary>
    [RelayCommand]
    private void NewNote() => NewNoteRequested?.Invoke();

    /// <summary>Asks to empty the trash.</summary>
    [RelayCommand]
    private void EmptyTrash() => EmptyTrashRequested?.Invoke();

    partial void OnSelectedItemChanged(NoteItemViewModel? value)
    {
        if (value is not null && !_isRestoring)
        {
            NoteChosen?.Invoke(value.Id);
        }
    }

    private async Task<(IReadOnlyList<NoteItemViewModel> Items, int Total)?> Read(NoteQuery query)
    {
        Failure = "";
        string dateStyle = _dateStyle();
        try
        {
            if (query.Text is not null)
            {
                Page<SearchResult> found = await Task.Run(() => _notes.Search(query));
                return (found.Items.Select(result => new NoteItemViewModel(result.Note, result.Snippet, dateStyle, _texts)).ToList(), found.Total);
            }

            Page<NoteSummary> listed = await Task.Run(() => _notes.List(query));
            return (listed.Items.Select(note => new NoteItemViewModel(note, null, dateStyle, _texts)).ToList(), listed.Total);
        }
        catch (AppException failure)
        {
            Failure = _failures.Text(failure);
            return null;
        }
    }

    private void Add(IEnumerable<NoteItemViewModel> items)
    {
        foreach (NoteItemViewModel item in items)
        {
            Items.Add(item);
        }
    }

    private void Show(int total)
    {
        HasMore = Items.Count < total;
        IsEmpty = total == 0;
        CountText = _texts.Count("list.count", total);
        EmptyText = _texts[_query.Text is not null ? "list.noResults" : _query.InTrash ? "list.trashEmpty" : "list.empty"];
    }
}
