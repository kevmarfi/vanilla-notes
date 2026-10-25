namespace VanillaNotes.Core.Storage;

/// <summary>
/// A folder of Markdown files the person picked to bring into a profile, as other editors of notes
/// leave them. It is read and never written. A folder whose name starts with a dot is the settings
/// of an application (Obsidian keeps them in .obsidian) or of a tool, and is passed over with
/// everything in it. Nothing outside the folder picked is ever read, whatever a note points at.
/// </summary>
public sealed class NoteFolder
{
    /// <summary>The endings of the files that are notes.</summary>
    private static readonly string[] _noteExtensions = [".md", ".markdown"];

    private readonly string _root;

    public NoteFolder(string root)
    {
        _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Every file of the folder and of the folders inside it, by its path inside the folder, in
    /// alphabetical order; the ones in folders that start with a dot left out.
    /// </summary>
    public IReadOnlyList<string> ListFiles() =>
        Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_root, path))
            .Where(relative => !relative.Split(Path.DirectorySeparatorChar).SkipLast(1).Any(folder => folder.StartsWith('.')))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Whether a file of the folder is a note, by its ending.</summary>
    public static bool IsNote(string relativePath) =>
        _noteExtensions.Contains(Path.GetExtension(relativePath), StringComparer.OrdinalIgnoreCase);

    /// <summary>The text of a file of the folder, read as UTF-8, with the mark some editors put at its start taken away.</summary>
    public string ReadText(string relativePath) => File.ReadAllText(Path.Combine(_root, relativePath));

    /// <summary>When a file of the folder was made and when it last changed, the first never after the second.</summary>
    public (DateTimeOffset Created, DateTimeOffset Updated) DatesOf(string relativePath)
    {
        string path = Path.Combine(_root, relativePath);
        DateTime updated = File.GetLastWriteTimeUtc(path);
        DateTime created = File.GetCreationTimeUtc(path);
        return (new DateTimeOffset(created < updated ? created : updated, TimeSpan.Zero), new DateTimeOffset(updated, TimeSpan.Zero));
    }

    /// <summary>
    /// Finds a file a note points at, by a path written from the folder of the note.
    /// </summary>
    /// <returns>The path of the file inside the folder, or nothing when it is not there or would be outside the folder picked.</returns>
    public string? Resolve(string noteRelativePath, string address)
    {
        string full = Path.GetFullPath(Path.Combine(_root, Path.GetDirectoryName(noteRelativePath) ?? "", address));
        bool inside = full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        return inside && File.Exists(full) ? Path.GetRelativePath(_root, full) : null;
    }

    /// <summary>The size in bytes of a file of the folder.</summary>
    public long SizeOf(string relativePath) => new FileInfo(Path.Combine(_root, relativePath)).Length;

    /// <summary>The bytes of a file of the folder.</summary>
    public byte[] Read(string relativePath) => File.ReadAllBytes(Path.Combine(_root, relativePath));
}
