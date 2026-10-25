namespace VanillaNotes.Core.Models;

/// <summary>A note as the data file of an export holds it: what it says, where it was, and when.</summary>
/// <param name="Title">Its title.</param>
/// <param name="Content">Its text, as Markdown.</param>
/// <param name="Folder">The name of its folder, or nothing for a note in none.</param>
/// <param name="Tags">Its tags.</param>
/// <param name="CreatedAt">When it was made.</param>
/// <param name="UpdatedAt">When it last changed.</param>
public sealed record ExportedNote(
    string Title,
    string Content,
    string? Folder,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
