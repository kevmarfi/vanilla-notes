using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Storage;
using VanillaNotes.Core.Validation;

namespace VanillaNotes.Core.Domain;

/// <summary>
/// The files attached to the notes of the open profile: attaching, reading, and removing them.
/// When the profile has a password, a file is sealed with its key before it reaches the disk.
/// </summary>
public sealed class AttachmentService
{
    private readonly AttachmentRepository _attachments;

    private readonly NoteRepository _notes;

    private readonly ProfileSession _session;

    private readonly DiskSpace _diskSpace;

    public AttachmentService(AttachmentRepository attachments, NoteRepository notes, ProfileSession session, DiskSpace diskSpace)
    {
        _attachments = attachments;
        _notes = notes;
        _session = session;
        _diskSpace = diskSpace;
    }

    /// <summary>The files of a note, in the order they were attached.</summary>
    public IReadOnlyList<Attachment> List(long noteId)
    {
        AssertNoteExists(noteId);
        return _attachments.ListForNote(noteId);
    }

    /// <summary>One attached file, as the note shows it.</summary>
    public Attachment Get(long id) => _attachments.Find(id) ?? throw NotFound();

    /// <summary>
    /// The bytes of an attached image, for a note to show it: nothing when there is no such
    /// attachment, or when it is a file that is not an image.
    /// </summary>
    public byte[]? ReadImage(long id)
    {
        Attachment? attachment = _attachments.Find(id);
        return attachment is not null && attachment.MimeType.StartsWith("image/", StringComparison.Ordinal) ? ReadContent(id) : null;
    }

    /// <summary>
    /// Attaches a file to a note. Nothing new is stored while the disk is running out of space.
    /// </summary>
    public Attachment Add(long noteId, string fileName, string mimeType, byte[] bytes)
    {
        string acceptedName = AttachmentValidator.FileName(fileName);
        string acceptedType = AttachmentValidator.MimeType(mimeType);
        AttachmentValidator.Size(bytes.LongLength);
        AssertNoteExists(noteId, writable: true);
        if (_diskSpace.IsLow())
        {
            throw new StorageException(ErrorCodes.StorageFull, "The disk is running out of space");
        }

        // The file first, then its record: a file without a record is housekeeping, a record without a file is a loss.
        string storageName = _session.SaveFile(bytes);
        try
        {
            return _attachments.Create(noteId, acceptedName, acceptedType, bytes.LongLength, storageName);
        }
        catch
        {
            _session.Files.Remove(storageName);
            throw;
        }
    }

    /// <summary>The bytes of an attached file, as they were attached.</summary>
    public byte[] ReadContent(long id)
    {
        string storageName = _attachments.FindStorageName(id) ?? throw NotFound();
        return _session.ReadFile(storageName);
    }

    /// <summary>Removes a file from its note, and from the disk.</summary>
    public void Remove(long id)
    {
        string storageName = _attachments.FindStorageName(id) ?? throw NotFound();
        _attachments.Remove(id);
        _session.Files.Remove(storageName);
    }

    /// <summary>
    /// Deletes the files of the profile that no record points at any longer: left behind by a
    /// write that was cut short.
    /// </summary>
    /// <returns>How many were deleted.</returns>
    public int RemoveOrphanFiles()
    {
        var recorded = new HashSet<string>(_attachments.ListStorageNames(), StringComparer.Ordinal);
        List<string> orphans = _session.Files.List().Where(name => !recorded.Contains(name)).ToList();
        foreach (string orphan in orphans)
        {
            _session.Files.Remove(orphan);
        }

        return orphans.Count;
    }

    /// <summary>The note is there; and, to add a file to it, not in the trash, where a note is only read.</summary>
    private void AssertNoteExists(long noteId, bool writable = false)
    {
        if (_notes.Find(noteId) is not Note note || (writable && note.DeletedAt is not null))
        {
            throw new NotFoundException("Note not found");
        }
    }

    private static NotFoundException NotFound() => new("Attachment not found");
}
