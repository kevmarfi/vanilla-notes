namespace VanillaNotes.App.ViewModels;

/// <summary>A note of the list dropped onto a place that takes notes.</summary>
/// <param name="NoteId">The number of the note.</param>
/// <param name="Target">What it was dropped onto: a folder of the sidebar, or nothing for the notes in no folder.</param>
public sealed record DroppedNote(long NoteId, object? Target);
