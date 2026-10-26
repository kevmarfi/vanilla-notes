using System.Globalization;
using System.Text.Json;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Export;

/// <summary>
/// Reads the data file of an export: the one the web version gives from its settings, which is
/// also the one this application writes. It holds the moment it was made and a list of notes,
/// each with its title, its text, the name of its folder, its tags, and its two dates.
/// </summary>
public static class ExportFileReader
{
    /// <summary>
    /// The notes of a data file. A file that is not one, or whose notes miss a part or carry one
    /// of the wrong kind, is refused whole.
    /// </summary>
    public static IReadOnlyList<ExportedNote> Read(byte[] file)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(file);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("notes", out JsonElement notes)
                || notes.ValueKind != JsonValueKind.Array)
            {
                throw NotAnExport();
            }

            return notes.EnumerateArray().Select(ReadNote).ToList();
        }
        catch (JsonException)
        {
            throw NotAnExport();
        }
    }

    private static ExportedNote ReadNote(JsonElement note)
    {
        if (note.ValueKind != JsonValueKind.Object)
        {
            throw NotAnExport();
        }

        JsonElement folder = Part(note, "folder");
        JsonElement tags = Part(note, "tags");
        if ((folder.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            || tags.ValueKind != JsonValueKind.Array
            || tags.EnumerateArray().Any(tag => tag.ValueKind != JsonValueKind.String))
        {
            throw NotAnExport();
        }

        return new ExportedNote(
            Text(note, "title"),
            Text(note, "content"),
            folder.GetString(),
            tags.EnumerateArray().Select(tag => tag.GetString()!).ToList(),
            Date(note, "createdAt"),
            Date(note, "updatedAt"));
    }

    private static JsonElement Part(JsonElement note, string name) =>
        note.TryGetProperty(name, out JsonElement part) ? part : throw NotAnExport();

    private static string Text(JsonElement note, string name)
    {
        JsonElement part = Part(note, name);
        return part.ValueKind == JsonValueKind.String ? part.GetString()! : throw NotAnExport();
    }

    private static DateTimeOffset Date(JsonElement note, string name) =>
        DateTimeOffset.TryParse(Text(note, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset moment)
            ? moment
            : throw NotAnExport();

    private static ValidationException NotAnExport() => new("exportFile", "The file is not the data file of an export of Vanilla Notes");
}
