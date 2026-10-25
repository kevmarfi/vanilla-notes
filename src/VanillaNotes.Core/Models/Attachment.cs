namespace VanillaNotes.Core.Models;

/// <summary>A file attached to a note, as the note shows it. Where it is kept on disk is not told.</summary>
/// <param name="Id">The number of the attachment.</param>
/// <param name="NoteId">The note it belongs to.</param>
/// <param name="FileName">The name the file had, only to show it and to save a copy under it.</param>
/// <param name="MimeType">What kind of file it is.</param>
/// <param name="Size">Its size in bytes, as it was given.</param>
/// <param name="CreatedAt">When it was attached.</param>
public sealed record Attachment(long Id, long NoteId, string FileName, string MimeType, long Size, DateTimeOffset CreatedAt);
