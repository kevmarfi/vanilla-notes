namespace VanillaNotes.Core.Models;

/// <summary>A folder and how many notes it holds.</summary>
/// <param name="Id">The number of the folder.</param>
/// <param name="Name">Its name, unique in the profile regardless of case.</param>
/// <param name="NoteCount">How many notes are in it.</param>
/// <param name="CreatedAt">When it was made.</param>
public sealed record Folder(long Id, string Name, int NoteCount, DateTimeOffset CreatedAt);
