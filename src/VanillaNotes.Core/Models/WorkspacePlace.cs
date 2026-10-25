namespace VanillaNotes.Core.Models;

/// <summary>
/// Where the person was in the workspace of a profile when they left it, to come back to it the
/// next time the profile is opened. Nothing chosen is the whole list with no note open.
/// </summary>
/// <param name="NoteId">The note that was open, if one was.</param>
/// <param name="FolderId">The folder chosen in the sidebar, if one was.</param>
/// <param name="WithoutFolder">Whether the notes in no folder were chosen.</param>
public sealed record WorkspacePlace(long? NoteId, long? FolderId, bool WithoutFolder);
