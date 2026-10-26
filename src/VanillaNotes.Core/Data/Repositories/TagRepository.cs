using Microsoft.Data.Sqlite;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Data.Repositories;

/// <summary>
/// The tags of the open profile that some note carries, each with how many notes carry it. Which
/// tags a note carries is written through the notes, by the repository of the notes.
/// </summary>
public sealed class TagRepository
{
    private readonly Database _database;

    public TagRepository(Database database)
    {
        _database = database;
    }

    // -- Reading -- //

    /// <summary>Every tag some note out of the trash carries, in alphabetical order, with how many of them do.</summary>
    public IReadOnlyList<Tag> ListInUse()
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.name, COUNT(*) AS note_count
            FROM tags t
            JOIN note_tags nt ON nt.tag_id = t.id
            JOIN notes n ON n.id = nt.note_id AND n.deleted_at IS NULL
            GROUP BY t.id
            ORDER BY t.name
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        var tags = new List<Tag>();
        while (reader.Read())
        {
            tags.Add(new Tag(reader.GetString(0), reader.GetInt32(1)));
        }

        return tags;
    }
}
