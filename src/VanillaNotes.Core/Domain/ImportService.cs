using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Export;
using VanillaNotes.Core.Markdown;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Storage;
using VanillaNotes.Core.Validation;

namespace VanillaNotes.Core.Domain;

/// <summary>
/// Brings into the open profile the notes of the data file the web version exports: each with
/// its folder, found by its name or made, its tags, and its dates. The notes the profile already
/// has are not touched. The attached files of the web version are not in that file, so they do
/// not come; the addresses that pointed at them are left in the text as they were. It also brings
/// in a folder of Markdown files, as other editors of notes leave them, with the images they point
/// at. It reads a whole file or folder and writes every note of it: it is run off the thread of the window.
/// </summary>
public sealed class ImportService
{
    private readonly NoteRepository _notes;

    private readonly FolderRepository _folders;

    private readonly AttachmentService _attachments;

    private readonly DiskSpace _diskSpace;

    public ImportService(NoteRepository notes, FolderRepository folders, AttachmentService attachments, DiskSpace diskSpace)
    {
        _notes = notes;
        _folders = folders;
        _attachments = attachments;
        _diskSpace = diskSpace;
    }

    /// <summary>
    /// Brings in the notes of a data file. Every note is checked before any is written, so that a
    /// file with one note that breaks a rule brings in none. Nothing is brought in while the disk
    /// is running out of space.
    /// </summary>
    /// <returns>How many notes were brought in.</returns>
    public int Import(byte[] file)
    {
        List<ExportedNote> accepted = ExportFileReader.Read(file)
            .Select(note => note with
            {
                Title = NoteValidator.Title(note.Title),
                Content = NoteValidator.Content(note.Content),
                Folder = note.Folder is null ? null : FolderValidator.Name(note.Folder),
                Tags = NoteValidator.Tags(note.Tags),
            })
            .ToList();
        if (_diskSpace.IsLow())
        {
            throw new StorageException(ErrorCodes.StorageFull, "The disk is running out of space");
        }

        // The names of folders are told apart regardless of case, as the database tells them apart.
        Dictionary<string, long> folderIds = _folders.List().ToDictionary(folder => folder.Name, folder => folder.Id, StringComparer.OrdinalIgnoreCase);
        foreach (ExportedNote note in accepted)
        {
            long? folderId = note.Folder is null ? null : FolderIdOf(note.Folder, folderIds);
            _notes.Create(note.Title, note.Content, folderId, note.Tags, note.CreatedAt, note.UpdatedAt);
        }

        return accepted.Count;
    }

    /// <summary>
    /// Brings in a folder of Markdown files. Each file is a note, titled by its name, with the
    /// dates of the file; the first folder inside the one picked is its folder, since folders have
    /// one level. An image it points at by a path inside the folder picked is attached to it, and
    /// its address written as the one of the attachment. Each note stands on its own: one left out
    /// leaves the others in. Nothing is brought in while the disk is running out of space.
    /// </summary>
    /// <param name="folder">The folder the person picked.</param>
    public FolderImport ImportFolder(string folder)
    {
        if (_diskSpace.IsLow())
        {
            throw new StorageException(ErrorCodes.StorageFull, "The disk is running out of space");
        }

        var source = new NoteFolder(folder);
        IReadOnlyList<string> files = source.ListFiles();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, long> folderIds = _folders.List().ToDictionary(existing => existing.Name, existing => existing.Id, StringComparer.OrdinalIgnoreCase);
        int notes = 0;
        int images = 0;
        foreach (string file in files.Where(NoteFolder.IsNote))
        {
            string content = source.ReadText(file);
            if (content.Length > NoteValidator.MaxContentLength)
            {
                continue;
            }

            string title = NoteValidator.Title(Shorten(Path.GetFileNameWithoutExtension(file).Trim(), NoteValidator.MaxTitleLength) is { Length: > 0 } name ? name : file);
            string? folderName = file.Contains(Path.DirectorySeparatorChar)
                ? FolderValidator.Name(Shorten(file[..file.IndexOf(Path.DirectorySeparatorChar)].Trim(), FolderValidator.MaxNameLength))
                : null;
            (DateTimeOffset created, DateTimeOffset updated) = source.DatesOf(file);
            Note note = _notes.Create(title, content, folderName is null ? null : FolderIdOf(folderName, folderIds), [], created, updated);
            used.Add(file);
            notes++;

            var attached = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            string rewritten = MarkdownParser.ReplaceImageAddresses(content, address =>
            {
                if (ImageOf(source, file, address) is not string image)
                {
                    return null;
                }

                if (!attached.TryGetValue(image, out long id))
                {
                    id = _attachments.Add(note.Id, Path.GetFileName(image), AttachmentValidator.MimeTypeOf(image), source.Read(image)).Id;
                    attached[image] = id;
                    used.Add(image);
                    images++;
                }

                return MarkdownParser.AttachmentAddress(id);
            });
            if (rewritten != content)
            {
                _notes.Rewrite(note.Id, rewritten);
            }
        }

        return new FolderImport(notes, images, files.Count(file => !used.Contains(file)));
    }

    /// <summary>
    /// The image a note of the folder points at, by its path inside the folder: an address of the
    /// web, of an attachment, or of the root of a disk is not one, nor a file of a kind or a size an
    /// attachment does not take.
    /// </summary>
    private static string? ImageOf(NoteFolder source, string note, string address)
    {
        if (address.Contains(':', StringComparison.Ordinal) || address.StartsWith('/') || address.StartsWith('\\') || address.StartsWith('#'))
        {
            return null;
        }

        string? image = source.Resolve(note, Uri.UnescapeDataString(address));
        bool isImage = image is not null
            && AttachmentValidator.MimeTypesByExtension.TryGetValue(Path.GetExtension(image), out string? mimeType)
            && mimeType.StartsWith("image/", StringComparison.Ordinal);
        return isImage && source.SizeOf(image!) is > 0 and <= AttachmentValidator.MaxSize ? image : null;
    }

    private static string Shorten(string text, int length) => text.Length <= length ? text : text[..length].TrimEnd();

    private long FolderIdOf(string name, Dictionary<string, long> folderIds)
    {
        if (!folderIds.TryGetValue(name, out long id))
        {
            id = (_folders.Create(name) ?? throw new ConflictException(ErrorCodes.FolderExists, "A folder with that name already exists")).Id;
            folderIds[name] = id;
        }

        return id;
    }
}
