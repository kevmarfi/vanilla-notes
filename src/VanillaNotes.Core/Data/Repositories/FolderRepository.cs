using Microsoft.Data.Sqlite;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Data.Repositories;

/// <summary>The folders of the open profile, each with how many notes it holds, the ones in the trash left out.</summary>
public sealed class FolderRepository
{
    private const string _select = """
        SELECT f.id, f.name, COUNT(n.id) AS note_count, f.created_at
        FROM folders f
        LEFT JOIN notes n ON n.folder_id = f.id AND n.deleted_at IS NULL
        """;

    private readonly Database _database;

    public FolderRepository(Database database)
    {
        _database = database;
    }

    // -- Reading -- //

    /// <summary>Every folder, in the order of their names.</summary>
    public IReadOnlyList<Folder> List()
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"{_select} GROUP BY f.id ORDER BY f.name, f.id";
        using SqliteDataReader reader = command.ExecuteReader();
        var folders = new List<Folder>();
        while (reader.Read())
        {
            folders.Add(ToFolder(reader));
        }

        return folders;
    }

    /// <summary>One folder, or nothing when there is none with that number.</summary>
    public Folder? Find(long id)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"{_select} WHERE f.id = $id GROUP BY f.id";
        command.Parameters.AddWithValue("$id", id);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ToFolder(reader) : null;
    }

    // -- Writing -- //

    /// <summary>
    /// Adds a folder.
    /// </summary>
    /// <returns>The folder, or nothing when another one already has that name regardless of case.</returns>
    public Folder? Create(string name)
    {
        long id;
        try
        {
            using SqliteConnection connection = _database.Connect();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO folders (name, created_at) VALUES ($name, $createdAt) RETURNING id";
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$createdAt", Rows.Now());
            id = (long)command.ExecuteScalar()!;
        }
        catch (SqliteException exception) when (Rows.IsUniqueViolation(exception))
        {
            return null;
        }

        return Find(id);
    }

    /// <summary>
    /// Gives a folder another name.
    /// </summary>
    /// <returns>The folder, or nothing when another one already has that name regardless of case.</returns>
    public Folder? Rename(long id, string name)
    {
        try
        {
            using SqliteConnection connection = _database.Connect();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE folders SET name = $name WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$name", name);
            command.ExecuteNonQuery();
        }
        catch (SqliteException exception) when (Rows.IsUniqueViolation(exception))
        {
            return null;
        }

        return Find(id);
    }

    /// <summary>
    /// Deletes a folder; its notes are left in no folder.
    /// </summary>
    /// <returns>Whether there was one to delete.</returns>
    public bool Remove(long id)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM folders WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() > 0;
    }

    private static Folder ToFolder(SqliteDataReader reader) =>
        new(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2), Rows.Date(reader, 3));
}
