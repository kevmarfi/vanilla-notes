using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Validation;

/// <summary>The values each setting of a profile accepts: the ones of the web version, and the times a private profile is closed after.</summary>
public static class SettingsValidator
{
    /// <summary>The languages of the interface.</summary>
    public static IReadOnlyList<string> Languages { get; } = ["en", "es"];

    /// <summary>The themes; no theme at all means the one of Windows.</summary>
    public static IReadOnlyList<string> Themes { get; } = ["light", "neutral", "dark"];

    /// <summary>The sizes of the text of a note.</summary>
    public static IReadOnlyList<string> TextSizes { get; } = ["small", "normal", "large"];

    /// <summary>The ways a note is written: visual, Markdown with its preview, and read only.</summary>
    public static IReadOnlyList<string> EditorModes { get; } = ["visual", "markdown", "read"];

    /// <summary>The ways the list of notes shows dates.</summary>
    public static IReadOnlyList<string> DateStyles { get; } = ["relative", "exact"];

    /// <summary>The minutes with nothing done after which a private profile is closed, or never.</summary>
    public static IReadOnlyList<string> LockTimes { get; } = ["5", "15", "30", "never"];

    /// <summary>The orders of the list of notes: by the last change, by when a note was made, or by title.</summary>
    public static IReadOnlyList<string> NoteOrders { get; } = ["updated", "created", "title"];

    /// <summary>
    /// Accepts settings whose every value is one of the ones its setting allows.
    /// </summary>
    public static ProfileSettings Settings(ProfileSettings settings)
    {
        OneOf("language", settings.Language, Languages);
        if (settings.Theme is not null)
        {
            OneOf("theme", settings.Theme, Themes);
        }

        OneOf("textSize", settings.TextSize, TextSizes);
        OneOf("editorMode", settings.EditorMode, EditorModes);
        OneOf("dateStyle", settings.DateStyle, DateStyles);
        OneOf("lockAfter", settings.LockAfter, LockTimes);
        OneOf("noteOrder", settings.NoteOrder, NoteOrders);
        return settings;
    }

    private static void OneOf(string field, string value, IReadOnlyList<string> accepted)
    {
        if (!accepted.Contains(value))
        {
            throw new ValidationException(field, $"{field} must be one of: {string.Join(", ", accepted)}");
        }
    }
}
