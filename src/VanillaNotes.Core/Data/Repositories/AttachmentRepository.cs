using Microsoft.Data.Sqlite;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Data.Repositories;

/// <summary>
/// The records of the files attached to the notes of the open profile. A record says what the
/// file is and under which random name it is stored; the file itself is kept by the file store.
/// </summary>
public sealed class AttachmentRepository
{
    private const string _columns = "id, note_id, filename, mime_type, size, created_at";

    private readonly Database _database;

    public AttachmentRepository(Database database)
    {
        _database = database;
    }

    // -- Reading -- //

    /// <summary>The files of a note, in the order they were attached.</summary>
    public IReadOnlyList<Attachment> ListForNote(long noteId)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {_columns} FROM attachments WHERE note_id = $noteId ORDER BY id";
        command.Parameters.AddWithValue("$noteId", noteId);
        using SqliteDataReader reader = command.ExecuteReader();
        var attachments = new List<Attachment>();
        while (reader.Read())
        {
            attachments.Add(ToAttachment(reader));
        }

        return attachments;
    }

    /// <summary>The names the files of a note are stored under.</summary>
    public IReadOnlyList<string> ListStorageNamesForNote(long noteId) =>
        ListStorageNames("SELECT storage_name FROM attachments WHERE note_id = $noteId", noteId);

    /// <summary>The names every file of the profile is stored under.</summary>
    public IReadOnlyList<string> ListStorageNames() => ListStorageNames("SELECT storage_name FROM attachments", noteId: null);

    /// <summary>One record, or nothing when there is none with that number.</summary>
    public Attachment? Find(long id)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {_columns} FROM attachments WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ToAttachment(reader) : null;
    }

    /// <summary>The name a file is stored under, or nothing when there is no record with that number.</summary>
    public string? FindStorageName(long id)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT storage_name FROM attachments WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() as string;
    }

    // -- Writing -- //

    /// <summary>Adds the record of a file already stored.</summary>
    public Attachment Create(long noteId, string fileName, string mimeType, long size, string storageName)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO attachments (note_id, filename, mime_type, size, storage_name, created_at)
            VALUES ($noteId, $fileName, $mimeType, $size, $storageName, $createdAt)
            RETURNING {_columns}
            """;
        command.Parameters.AddWithValue("$noteId", noteId);
        command.Parameters.AddWithValue("$fileName", fileName);
        command.Parameters.AddWithValue("$mimeType", mimeType);
        command.Parameters.AddWithValue("$size", size);
        command.Parameters.AddWithValue("$storageName", storageName);
        command.Parameters.AddWithValue("$createdAt", Rows.Now());
        using SqliteDataReader reader = command.ExecuteReader();
        reader.Read();
        return ToAttachment(reader);
    }

    /// <summary>
    /// Deletes the record of a file. The file is for the caller to delete, after.
    /// </summary>
    /// <returns>Whether there was a record to delete.</returns>
    public bool Remove(long id)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM attachments WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() > 0;
    }

    private List<string> ListStorageNames(string sql, long? noteId)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        if (noteId is long id)
        {
            command.Parameters.AddWithValue("$noteId", id);
        }

        using SqliteDataReader reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static Attachment ToAttachment(SqliteDataReader reader) =>
        new(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4), Rows.Date(reader, 5));
}
