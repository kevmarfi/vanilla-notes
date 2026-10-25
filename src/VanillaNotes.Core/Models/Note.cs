namespace VanillaNotes.Core.Models;

/// <summary>A whole note, as it is opened: its text, where it is filed, and its tags.</summary>
/// <param name="Id">The number of the note.</param>
/// <param name="Title">The title.</param>
/// <param name="Content">The text, as Markdown.</param>
/// <param name="FolderId">The folder it is in, or nothing when it is in none.</param>
/// <param name="Tags">Its tags, in lowercase and in alphabetical order.</param>
/// <param name="Version">How many times its title or text was saved; a save based on another version is refused.</param>
/// <param name="CreatedAt">When it was made.</param>
/// <param name="UpdatedAt">When its title or text last changed.</param>
/// <param name="DeletedAt">When it was moved to the trash, or nothing when it is not there.</param>
/// <param name="IsPinned">Whether it is pinned, and so comes first in the list.</param>
public sealed record Note(
    long Id,
    string Title,
    string Content,
    long? FolderId,
    IReadOnlyList<string> Tags,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt = null,
    bool IsPinned = false);
