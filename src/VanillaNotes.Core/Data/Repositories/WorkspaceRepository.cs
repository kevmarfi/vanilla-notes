using Microsoft.Data.Sqlite;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Data.Repositories;

/// <summary>
/// Where the person was in the workspace of the open profile when they left it, kept as one row.
/// A note or a folder deleted since is forgotten by the database itself, and a note in the trash
/// is read as none.
/// </summary>
public sealed class WorkspaceRepository
{
    private readonly Database _database;

    public WorkspaceRepository(Database database)
    {
        _database = database;
    }

    // -- Reading -- //

    /// <summary>Where the person was, or nothing when the profile was never left with something chosen.</summary>
    public WorkspacePlace? Find()
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN n.deleted_at IS NULL THEN w.note_id END, w.folder_id, w.without_folder
            FROM workspace w
            LEFT JOIN notes n ON n.id = w.note_id
            WHERE w.id = 1
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? new WorkspacePlace(Rows.NullableLong(reader, 0), Rows.NullableLong(reader, 1), reader.GetInt64(2) == 1) : null;
    }

    // -- Writing -- //

    /// <summary>
    /// Stores where the person is, in place of where they were. A note or a folder that is no
    /// longer there is stored as none.
    /// </summary>
    public void Save(WorkspacePlace place)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO workspace (id, note_id, folder_id, without_folder, updated_at)
            VALUES (1, (SELECT id FROM notes WHERE id = $noteId), (SELECT id FROM folders WHERE id = $folderId), $withoutFolder, $updatedAt)
            ON CONFLICT (id) DO UPDATE SET
              note_id = excluded.note_id,
              folder_id = excluded.folder_id,
              without_folder = excluded.without_folder,
              updated_at = excluded.updated_at
            """;
        command.Parameters.AddWithValue("$noteId", (object?)place.NoteId ?? DBNull.Value);
        command.Parameters.AddWithValue("$folderId", (object?)place.FolderId ?? DBNull.Value);
        command.Parameters.AddWithValue("$withoutFolder", place.WithoutFolder && place.FolderId is null ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", Rows.Now());
        command.ExecuteNonQuery();
    }
}
