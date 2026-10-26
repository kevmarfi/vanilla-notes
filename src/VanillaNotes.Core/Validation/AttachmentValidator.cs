using VanillaNotes.Core.Errors;

namespace VanillaNotes.Core.Validation;

/// <summary>What a file attached to a note accepts: its name, its kind, and its size.</summary>
public static class AttachmentValidator
{
    /// <summary>The longest the name of a file may be.</summary>
    public const int MaxFileNameLength = 255;

    /// <summary>The largest a file may be: 5 MB, as in the web version.</summary>
    public const long MaxSize = 5 * 1024 * 1024;

    /// <summary>The kinds of file a note accepts: the images a note can show, PDF documents, and plain text.</summary>
    public static IReadOnlyList<string> AllowedMimeTypes { get; } = ["image/png", "image/jpeg", "image/gif", "image/webp", "application/pdf", "text/plain"];

    /// <summary>
    /// The kind of each file a note accepts, by the ending of its name: a file picked on the disk
    /// says what it is by its name alone, as it did in the browser of the web version.
    /// </summary>
    public static IReadOnlyDictionary<string, string> MimeTypesByExtension { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
    };

    /// <summary>
    /// Accepts the name of a file: 1 to 255 characters, with no slash of either kind, since a
    /// name is only shown and never used as a path.
    /// </summary>
    public static string FileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ValidationException("fileName", "File name is required");
        }

        if (fileName.Length > MaxFileNameLength)
        {
            throw new ValidationException("fileName", $"File name must be at most {MaxFileNameLength} characters long");
        }

        if (fileName.Contains('/', StringComparison.Ordinal) || fileName.Contains('\\', StringComparison.Ordinal))
        {
            throw new ValidationException("fileName", "File name must not contain slashes");
        }

        return fileName;
    }

    /// <summary>
    /// Accepts the kind of a file when it is one of the allowed ones.
    /// </summary>
    /// <returns>The kind in lowercase.</returns>
    public static string MimeType(string? mimeType)
    {
        string lowered = mimeType?.Trim().ToLowerInvariant() ?? "";
        if (!AllowedMimeTypes.Contains(lowered))
        {
            throw new ValidationException("mimeType", $"File must be one of: {string.Join(", ", AllowedMimeTypes)}");
        }

        return lowered;
    }

    /// <summary>
    /// The kind of a file, told by the ending of its name, when it is one a note accepts.
    /// </summary>
    public static string MimeTypeOf(string fileName)
    {
        if (!MimeTypesByExtension.TryGetValue(Path.GetExtension(fileName), out string? mimeType))
        {
            throw new ValidationException("mimeType", $"File must be one of: {string.Join(", ", MimeTypesByExtension.Keys)}");
        }

        return mimeType;
    }

    /// <summary>
    /// Accepts the size of a file: at least one byte, and 5 MB at most.
    /// </summary>
    public static long Size(long size)
    {
        if (size < 1)
        {
            throw new ValidationException("file", "The file is empty");
        }

        if (size > MaxSize)
        {
            throw new ValidationException("file", "The file must be at most 5 MB");
        }

        return size;
    }
}
