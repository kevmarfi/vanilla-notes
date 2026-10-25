namespace VanillaNotes.Core.Models;

/// <summary>
/// What the list of notes is asked for: the notes of a folder, of no folder, or of a tag, the
/// ones a text is found in, and which page of them; or the notes in the trash. Nothing asked is
/// every note that is not in the trash.
/// </summary>
/// <param name="FolderId">Only the notes of this folder.</param>
/// <param name="WithoutFolder">Only the notes that are in no folder.</param>
/// <param name="Tag">Only the notes that carry this tag.</param>
/// <param name="Text">Only the notes where every word of this text is found, best first.</param>
/// <param name="Limit">How many notes a page holds.</param>
/// <param name="Offset">How many notes come before the page.</param>
/// <param name="InTrash">Only the notes in the trash, the last moved there first, instead of the others.</param>
/// <param name="Order">The order of a list, after the notes pinned: by the last change, by when a note was made, or by title. A search and the trash keep their own.</param>
public sealed record NoteQuery(
    long? FolderId = null,
    bool WithoutFolder = false,
    string? Tag = null,
    string? Text = null,
    int Limit = 50,
    int Offset = 0,
    bool InTrash = false,
    string Order = "updated");
