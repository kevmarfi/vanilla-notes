using System.Text.Json;
using Microsoft.Data.Sqlite;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Data.Repositories;

/// <summary>
/// The settings of the open profile, kept as one JSON object in its database. A setting that is
/// not in the object, because it was never changed or did not exist when it was stored, has its
/// default.
/// </summary>
public sealed class ProfileSettingsRepository
{
    private readonly Database _database;

    public ProfileSettingsRepository(Database database)
    {
        _database = database;
    }

    // -- Reading -- //

    /// <summary>The settings of the profile, or nothing when it never stored any.</summary>
    public ProfileSettings? Find()
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT settings FROM settings WHERE id = 1";
        return command.ExecuteScalar() is string json ? FromJson(json) : null;
    }

    // -- Writing -- //

    /// <summary>Stores the settings of the profile, in place of the ones it had.</summary>
    public void Save(ProfileSettings settings)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings (id, settings, updated_at) VALUES (1, $settings, $updatedAt)
            ON CONFLICT (id) DO UPDATE SET settings = excluded.settings, updated_at = excluded.updated_at
            """;
        command.Parameters.AddWithValue("$settings", ToJson(settings));
        command.Parameters.AddWithValue("$updatedAt", Rows.Now());
        command.ExecuteNonQuery();
    }

    // -- JSON -- //

    private static string ToJson(ProfileSettings settings) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["language"] = settings.Language,
            ["theme"] = settings.Theme,
            ["textSize"] = settings.TextSize,
            ["editorMode"] = settings.EditorMode,
            ["spellcheck"] = settings.Spellcheck,
            ["dateStyle"] = settings.DateStyle,
            ["lockAfter"] = settings.LockAfter,
            ["noteOrder"] = settings.NoteOrder,
        });

    private static ProfileSettings FromJson(string json)
    {
        ProfileSettings defaults = ProfileSettings.Default;
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return new ProfileSettings(
            Text(root, "language") ?? defaults.Language,
            root.TryGetProperty("theme", out JsonElement theme) && theme.ValueKind == JsonValueKind.String ? theme.GetString() : defaults.Theme,
            Text(root, "textSize") ?? defaults.TextSize,
            Text(root, "editorMode") ?? defaults.EditorMode,
            root.TryGetProperty("spellcheck", out JsonElement spellcheck) && spellcheck.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? spellcheck.GetBoolean()
                : defaults.Spellcheck,
            Text(root, "dateStyle") ?? defaults.DateStyle,
            Text(root, "lockAfter") ?? defaults.LockAfter,
            Text(root, "noteOrder") ?? defaults.NoteOrder);
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
