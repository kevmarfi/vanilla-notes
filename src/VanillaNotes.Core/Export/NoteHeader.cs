using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Export;

/// <summary>
/// The short header an exported Markdown file starts with, as the web version writes it: the
/// title, the tags, and the dates of the note, between two lines of three hyphens.
/// </summary>
public static class NoteHeader
{
    /// <summary>The form dates take in an export: ISO 8601, in UTC, to the millisecond.</summary>
    public const string DateFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    /// <summary>How text is written as JSON in an export: as the web version writes it, with quotes escaped by a backslash and every other character as it is.</summary>
    public static JsonSerializerOptions Json { get; } = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Writes the header of a note, with the line that ends it.
    /// </summary>
    public static string Write(Note note) =>
        string.Join(
            '\n',
            "---",
            $"title: {JsonSerializer.Serialize(note.Title, Json)}",
            $"tags: [{string.Join(", ", note.Tags)}]",
            $"created: {Date(note.CreatedAt)}",
            $"updated: {Date(note.UpdatedAt)}",
            "---");

    /// <summary>A date in the form an export writes it.</summary>
    public static string Date(DateTimeOffset moment) => moment.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);
}
