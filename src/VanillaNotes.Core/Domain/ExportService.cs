using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Export;
using VanillaNotes.Core.Markdown;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Domain;

/// <summary>
/// Takes every note of the open profile out of the application, with the attached files the
/// notes point at, in the form the person chose. It reads the whole profile: it is run off the
/// thread of the window.
/// </summary>
public sealed class ExportService
{
    private readonly NoteRepository _notes;

    private readonly FolderRepository _folders;

    private readonly AttachmentRepository _attachments;

    private readonly ProfileSession _session;

    public ExportService(NoteRepository notes, FolderRepository folders, AttachmentRepository attachments, ProfileSession session)
    {
        _notes = notes;
        _folders = folders;
        _attachments = attachments;
        _session = session;
    }

    /// <summary>
    /// Writes the export of every note of the profile.
    /// </summary>
    /// <returns>The bytes of the archive or of the data file.</returns>
    public byte[] Export(ExportFormat format)
    {
        IReadOnlyList<Note> notes = _notes.ListAll();
        Dictionary<long, string> folderNames = _folders.List().ToDictionary(folder => folder.Id, folder => folder.Name);
        var files = new Dictionary<long, (string MimeType, byte[] Bytes)>();
        if (format != ExportFormat.Data)
        {
            // A note points at its own files only; an address that points at no file is left as it is.
            foreach (long id in MarkdownParser.FindAttachmentIds(notes.Select(note => note.Content)))
            {
                if (_attachments.Find(id) is Attachment attachment && _attachments.FindStorageName(id) is string storageName)
                {
                    files[id] = (attachment.MimeType, _session.ReadFile(storageName));
                }
            }
        }

        return NoteExporter.Export(notes, folderNames, files, format, DateTimeOffset.UtcNow);
    }
}
