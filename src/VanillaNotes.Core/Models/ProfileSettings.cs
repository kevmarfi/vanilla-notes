namespace VanillaNotes.Core.Models;

/// <summary>
/// The settings of the interface a profile keeps: the ones of the web version, with the same
/// values, and how long a private profile stays open with nothing done. A setting the person never
/// changed has its default.
/// </summary>
/// <param name="Language">The language of the interface: English or Spanish.</param>
/// <param name="Theme">The theme: light, neutral, or dark; nothing means the one Windows uses.</param>
/// <param name="TextSize">The size of the text of a note: small, normal, or large.</param>
/// <param name="EditorMode">The way a note is written: visual, Markdown with its preview, or read only.</param>
/// <param name="Spellcheck">Whether the spelling of what is written is checked.</param>
/// <param name="DateStyle">How the list of notes shows dates: relative to now, or exact.</param>
/// <param name="LockAfter">The minutes with nothing done after which a private profile is closed (5, 15, or 30), or never.</param>
/// <param name="NoteOrder">The order of the list of notes, after the ones pinned: by the last change, by when a note was made, or by title.</param>
public sealed record ProfileSettings(
    string Language,
    string? Theme,
    string TextSize,
    string EditorMode,
    bool Spellcheck,
    string DateStyle,
    string LockAfter = "15",
    string NoteOrder = "updated")
{
    /// <summary>The settings of a profile that never changed any: English, the theme of Windows, normal text, the visual way, spelling checked, relative dates, closed after 15 minutes with nothing done, the list by the last change.</summary>
    public static ProfileSettings Default { get; } = new("en", null, "normal", "visual", true, "relative");
}
