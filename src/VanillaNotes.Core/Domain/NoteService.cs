using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Storage;
using VanillaNotes.Core.Validation;

namespace VanillaNotes.Core.Domain;

/// <summary>
/// The notes of the open profile: their list and their search, every way a note is written, and
/// the trash. Whatever the way of writing, a note is saved here and nowhere else. A note in the
/// trash can be read, put back, or deleted for good, and nothing else: writing it is refused as if
/// it were not there.
/// </summary>
public sealed class NoteService
{
    /// <summary>How long a note stays in the trash before it is deleted for good: 30 days.</summary>
    public static readonly TimeSpan TrashKeeping = TimeSpan.FromDays(30);

    private readonly NoteRepository _notes;

    private readonly FolderRepository _folders;

    private readonly AttachmentRepository _attachments;

    private readonly ProfileSession _session;

    private readonly DiskSpace _diskSpace;

    public NoteService(NoteRepository notes, FolderRepository folders, AttachmentRepository attachments, ProfileSession session, DiskSpace diskSpace)
    {
        _notes = notes;
        _folders = folders;
        _attachments = attachments;
        _session = session;
        _diskSpace = diskSpace;
    }

    /// <summary>
    /// One page of the notes a query asks for, most recently changed first. A query with a search
    /// text is a search, not a list.
    /// </summary>
    public Page<NoteSummary> List(NoteQuery query)
    {
        NoteQuery accepted = NoteValidator.Query(query);
        if (accepted.Text is not null)
        {
            throw new ValidationException("text", "A list takes no search text; that is a search");
        }

        return _notes.List(accepted);
    }

    /// <summary>
    /// One page of the notes where every word of the search text is found, in the title or the
    /// text, best first, and narrowed by the filters of the query.
    /// </summary>
    public Page<SearchResult> Search(NoteQuery query)
    {
        NoteQuery accepted = NoteValidator.Query(query);
        if (accepted.Text is null)
        {
            throw new ValidationException("text", "Search text must be between 1 and 100 characters long");
        }

        return _notes.Search(accepted);
    }

    /// <summary>One note, whole, in the trash or not.</summary>
    public Note Get(long id) => _notes.Find(id) ?? throw NotFound();

    /// <summary>
    /// Makes a note, in a folder or in none, with its tags. Nothing new is stored while the disk
    /// is running out of space.
    /// </summary>
    public Note Create(string title, string? content = "", long? folderId = null, IEnumerable<string>? tags = null)
    {
        string acceptedTitle = NoteValidator.Title(title);
        string acceptedContent = NoteValidator.Content(content);
        IReadOnlyList<string> acceptedTags = NoteValidator.Tags(tags);
        AssertFolderExists(folderId);
        if (_diskSpace.IsLow())
        {
            throw new StorageException(ErrorCodes.StorageFull, "The disk is running out of space");
        }

        return _notes.Create(acceptedTitle, acceptedContent, folderId, acceptedTags);
    }

    /// <summary>
    /// Saves the title and the text of a note, when it has not changed since the version they
    /// were based on. Saving what the note already says changes nothing.
    /// </summary>
    /// <param name="version">The version of the note the title and the text were written on.</param>
    public Note Edit(long id, int version, string title, string? content)
    {
        string acceptedTitle = NoteValidator.Title(title);
        string acceptedContent = NoteValidator.Content(content);
        Note note = GetOutOfTrash(id);
        if (note.Version != version)
        {
            throw Conflict();
        }

        if (note.Title == acceptedTitle && note.Content == acceptedContent)
        {
            return note;
        }

        return _notes.Update(id, version, acceptedTitle, acceptedContent) ?? throw Conflict();
    }

    /// <summary>Files a note in a folder, or in none. Filing is not editing: the version does not move.</summary>
    public Note Move(long id, long? folderId)
    {
        GetOutOfTrash(id);
        AssertFolderExists(folderId);
        _notes.SetFolder(id, folderId);
        return Get(id);
    }

    /// <summary>Pins a note, so that it comes first in the list, or unpins it. Pinning is not editing: the version does not move.</summary>
    public Note Pin(long id, bool pinned)
    {
        GetOutOfTrash(id);
        _notes.SetPinned(id, pinned);
        return Get(id);
    }

    /// <summary>Puts a list of tags in place of the ones a note carries. Tagging is not editing: the version does not move.</summary>
    public Note SetTags(long id, IEnumerable<string>? tags)
    {
        IReadOnlyList<string> accepted = NoteValidator.Tags(tags);
        GetOutOfTrash(id);
        _notes.SetTags(id, accepted);
        return Get(id);
    }

    // -- The trash -- //

    /// <summary>Moves a note to the trash, with its attachments, its tags, and its folder, as it is.</summary>
    public void Trash(long id)
    {
        GetOutOfTrash(id);
        _notes.SetTrashed(id, trashed: true);
    }

    /// <summary>Puts a note back from the trash as it was, in its folder when the folder is still there.</summary>
    public Note Restore(long id)
    {
        if (Get(id).DeletedAt is null)
        {
            throw NotFound();
        }

        _notes.SetTrashed(id, trashed: false);
        return Get(id);
    }

    /// <summary>Deletes for good every note in the trash, with its attachments.</summary>
    /// <returns>How many notes were deleted.</returns>
    public int EmptyTrash() => RemoveAll(_notes.ListTrashedIds());

    /// <summary>Deletes for good the notes that have been in the trash longer than it keeps them.</summary>
    /// <returns>How many notes were deleted.</returns>
    public int RemoveExpired() => RemoveAll(_notes.ListTrashedIds(DateTimeOffset.UtcNow - TrashKeeping));

    /// <summary>Deletes a note for good, in the trash or not, with its attachments, files included.</summary>
    public void Remove(long id)
    {
        Get(id);

        // Read before deleting: once the note is gone, nothing says which files were its own.
        IReadOnlyList<string> files = _attachments.ListStorageNamesForNote(id);
        _notes.Remove(id);
        foreach (string file in files)
        {
            _session.Files.Remove(file);
        }
    }

    private int RemoveAll(IReadOnlyList<long> ids)
    {
        foreach (long id in ids)
        {
            Remove(id);
        }

        return ids.Count;
    }

    /// <summary>A note that can be written: one that is there and not in the trash.</summary>
    private Note GetOutOfTrash(long id) => Get(id) is { DeletedAt: null } note ? note : throw NotFound();

    private void AssertFolderExists(long? folderId)
    {
        if (folderId is long id && _folders.Find(id) is null)
        {
            throw new ValidationException("folderId", "Folder does not exist");
        }
    }

    private static NotFoundException NotFound() => new("Note not found");

    private static ConflictException Conflict() => new(ErrorCodes.VersionConflict, "The note has changed since that version");
}
