namespace VanillaNotes.Core.Models;

/// <summary>A tag and how many notes carry it. A tag no note carries does not exist.</summary>
/// <param name="Name">The tag, in lowercase.</param>
/// <param name="NoteCount">How many notes carry it.</param>
public sealed record Tag(string Name, int NoteCount);
