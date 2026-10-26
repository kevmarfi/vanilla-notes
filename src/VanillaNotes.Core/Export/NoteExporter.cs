using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VanillaNotes.Core.Markdown;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Export;

/// <summary>
/// Writes what an export takes out of a profile, in the form chosen: Markdown files or web pages
/// in a ZIP archive, or a single data file. What each holds is the one of the web version.
/// </summary>
public static partial class NoteExporter
{
    /// <summary>The folder of an archive that holds the attached files the notes point at.</summary>
    private const string _attachmentsFolder = "attachments";

    /// <summary>The longest a name of a file or folder made from a title may be.</summary>
    private const int _maxNameLength = 100;

    /// <summary>The name of a file whose title leaves nothing a file name may hold.</summary>
    private const string _fallbackName = "untitled";

    /// <summary>The extension of the file of each kind of attachment, in the archive of Markdown files.</summary>
    private static readonly Dictionary<string, string> _attachmentExtensions = new(StringComparer.Ordinal)
    {
        ["image/png"] = "png",
        ["image/jpeg"] = "jpg",
        ["image/gif"] = "gif",
        ["image/webp"] = "webp",
        ["application/pdf"] = "pdf",
        ["text/plain"] = "txt",
    };

    /// <summary>How the data file is written: indented by two, as the web version writes it.</summary>
    private static readonly JsonSerializerOptions _json = new(NoteHeader.Json) { WriteIndented = true, IndentSize = 2 };

    /// <summary>The look of an exported web page: the one of the web version.</summary>
    private const string _pageStyle = """

            body { margin: 0; background: #f5f6f5; color: #171a18; font: 16px/1.6 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; }
            main { max-width: 72ch; margin: 0 auto; background: #ffffff; padding: 32px 24px; overflow-wrap: anywhere; }
            h1, h2, h3, h4, h5, h6 { margin: 1.2em 0 0.4em; line-height: 1.25; }
            main > h1:first-child { margin-top: 0; }
            a { color: #166534; }
            img { max-width: 100%; }
            blockquote { margin: 1em 0; border-left: 3px solid #d8dcd9; padding-left: 12px; color: #5d6660; }
            code { border-radius: 6px; background: #eef0ee; padding: 0.1em 0.35em; font: 0.9em ui-monospace, Consolas, Menlo, monospace; }
            pre { overflow-x: auto; border-radius: 8px; background: #eef0ee; padding: 12px 14px; }
            pre code { background: none; padding: 0; }
            table { border-collapse: collapse; }
            th, td { border: 1px solid #d8dcd9; padding: 4px 10px; }
            hr { border: 0; border-top: 1px solid #d8dcd9; }
            .token--comment { color: #6f7772; font-style: italic; }
            .token--string { color: #2f6b3a; }
            .token--number { color: #9a4d12; }
            .token--keyword { color: #7b3aa0; }
            @media print { body, main { background: none; padding: 0; } }
        """;

    /// <summary>
    /// Writes the export of some notes.
    /// </summary>
    /// <param name="notes">The notes, in the order they are written.</param>
    /// <param name="folderNames">The name of each folder, by its number.</param>
    /// <param name="files">The attached files the notes point at, by the number of the attachment: their kind, and their bytes.</param>
    /// <param name="exportedAt">The moment of the export, written in the data file.</param>
    /// <returns>The bytes of the archive or of the data file.</returns>
    public static byte[] Export(
        IReadOnlyList<Note> notes,
        IReadOnlyDictionary<long, string> folderNames,
        IReadOnlyDictionary<long, (string MimeType, byte[] Bytes)> files,
        ExportFormat format,
        DateTimeOffset exportedAt) =>
        format switch
        {
            ExportFormat.Markdown => MarkdownArchive(notes, folderNames, files),
            ExportFormat.Html => HtmlArchive(notes, folderNames, files),
            ExportFormat.Data => DataFile(notes, folderNames, exportedAt),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

    /// <summary>The extension of the file an export is saved as: an archive, or the data file.</summary>
    public static string Extension(ExportFormat format) => format == ExportFormat.Data ? ".json" : ".zip";

    // -- The three forms -- //

    private static byte[] MarkdownArchive(IReadOnlyList<Note> notes, IReadOnlyDictionary<long, string> folderNames, IReadOnlyDictionary<long, (string MimeType, byte[] Bytes)> files)
    {
        Dictionary<long, string> attachmentPaths = files
            .Where(file => _attachmentExtensions.ContainsKey(file.Value.MimeType))
            .ToDictionary(file => file.Key, file => $"{_attachmentsFolder}/{file.Key}.{_attachmentExtensions[file.Value.MimeType]}");

        return Archive(archive =>
        {
            List<string> paths = NotePaths(notes, folderNames, ".md");
            for (int i = 0; i < notes.Count; i++)
            {
                Note note = notes[i];

                // A note inside a folder is one level down from the attachments.
                string up = note.FolderId is null ? "" : "../";
                string text = MarkdownParser.ReplaceAttachmentAddresses(note.Content, id => attachmentPaths.TryGetValue(id, out string? path) ? up + path : null);
                Add(archive, paths[i], Encoding.UTF8.GetBytes($"{NoteHeader.Write(note)}\n\n{text}\n"), note.UpdatedAt);
            }

            foreach ((long id, string path) in attachmentPaths)
            {
                Add(archive, path, files[id].Bytes, DateTimeOffset.UtcNow);
            }
        });
    }

    private static byte[] HtmlArchive(IReadOnlyList<Note> notes, IReadOnlyDictionary<long, string> folderNames, IReadOnlyDictionary<long, (string MimeType, byte[] Bytes)> files)
    {
        // Only images are shown, carried inside the page; nothing is fetched from anywhere.
        Dictionary<long, string> imageSources = files
            .Where(file => file.Value.MimeType.StartsWith("image/", StringComparison.Ordinal))
            .ToDictionary(file => file.Key, file => $"data:{file.Value.MimeType};base64,{Convert.ToBase64String(file.Value.Bytes)}");

        return Archive(archive =>
        {
            List<string> paths = NotePaths(notes, folderNames, ".html");
            for (int i = 0; i < notes.Count; i++)
            {
                Note note = notes[i];
                string body = MarkdownHtmlWriter.Write(note.Content, id => imageSources.GetValueOrDefault(id));
                Add(archive, paths[i], Encoding.UTF8.GetBytes(Page(note, body)), note.UpdatedAt);
            }
        });
    }

    private static byte[] DataFile(IReadOnlyList<Note> notes, IReadOnlyDictionary<long, string> folderNames, DateTimeOffset exportedAt)
    {
        var data = new Dictionary<string, object>
        {
            ["exportedAt"] = NoteHeader.Date(exportedAt),
            ["notes"] = notes.Select(note => new Dictionary<string, object?>
            {
                ["title"] = note.Title,
                ["content"] = note.Content,
                ["folder"] = note.FolderId is long id ? folderNames.GetValueOrDefault(id) : null,
                ["tags"] = note.Tags,
                ["createdAt"] = NoteHeader.Date(note.CreatedAt),
                ["updatedAt"] = NoteHeader.Date(note.UpdatedAt),
            }).ToList(),
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, _json) + "\n");
    }

    // -- Helpers -- //

    private static string Page(Note note, string body)
    {
        string title = WebUtility.HtmlEncode(note.Title);
        return $"""
            <!doctype html>
            <html>
              <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta name="keywords" content="{WebUtility.HtmlEncode(string.Join(", ", note.Tags))}">
                <meta name="created" content="{NoteHeader.Date(note.CreatedAt)}">
                <meta name="date" content="{NoteHeader.Date(note.UpdatedAt)}">
                <title>{title}</title>
                <style>{_pageStyle}
                </style>
              </head>
              <body>
                <main>
                  <h1>{title}</h1>
                  {body}
                </main>
              </body>
            </html>

            """;
    }

    /// <summary>
    /// The path of each note in an archive: its title in the folder of its folder, made safe as a
    /// file name, and numbered when another note already took it regardless of case.
    /// </summary>
    private static List<string> NotePaths(IReadOnlyList<Note> notes, IReadOnlyDictionary<long, string> folderNames, string extension)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new List<string>();
        foreach (Note note in notes)
        {
            string folder = note.FolderId is long id && folderNames.TryGetValue(id, out string? name) ? $"{FileName(name)}/" : "";
            string stem = $"{folder}{FileName(note.Title)}";
            string path = stem + extension;
            for (int copy = 2; !taken.Add(path); copy++)
            {
                path = $"{stem} ({copy}){extension}";
            }

            paths.Add(path);
        }

        return paths;
    }

    /// <summary>
    /// A text made safe as the name of a file or a folder on any system: no character a name may
    /// not hold, no dot or space at either end, and 100 characters at most.
    /// </summary>
    private static string FileName(string text)
    {
        string replaced = ForbiddenInNames().Replace(text, "-");
        string cut = replaced.Length > _maxNameLength ? replaced[.._maxNameLength] : replaced;
        string trimmed = cut.Trim('.', ' ', '\t');
        return trimmed.Length == 0 ? _fallbackName : trimmed;
    }

    private static byte[] Archive(Action<ZipArchive> write)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            write(archive);
        }

        return stream.ToArray();
    }

    private static void Add(ZipArchive archive, string path, byte[] bytes, DateTimeOffset modifiedAt)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);

        // An archive cannot hold a date before 1980.
        entry.LastWriteTime = modifiedAt.Year < 1980 ? new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero) : modifiedAt;
        using Stream content = entry.Open();
        content.Write(bytes);
    }

    [GeneratedRegex(@"[\\/:*?""<>|\u0000-\u001f]")]
    private static partial Regex ForbiddenInNames();
}
