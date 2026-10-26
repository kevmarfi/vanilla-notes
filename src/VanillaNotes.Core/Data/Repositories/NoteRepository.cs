using System.Text;
using Microsoft.Data.Sqlite;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Data.Repositories;

/// <summary>
/// The notes of the open profile, with the tags each carries: their list and its filters, the
/// search over title and text, the trash, and every write of a note. The tags of a note are
/// written here, together with the note, since they are reached only through it. A note in the
/// trash is read only when it is asked for by its number, or by a list of the trash.
/// </summary>
public sealed class NoteRepository
{
    /// <summary>Everything the list shows of a note, with its tags gathered in alphabetical order.</summary>
    private const string _summaryColumns = """
        n.id, n.title, n.folder_id, n.version, n.created_at, n.updated_at,
        (SELECT json_group_array(name) FROM (
          SELECT t.name FROM note_tags nt JOIN tags t ON t.id = nt.tag_id
          WHERE nt.note_id = n.id ORDER BY t.name
        )) AS tags,
        n.deleted_at, n.pinned
        """;

    /// <summary>The fragment of the text around the words that matched: plain text, an ellipsis where it was cut, twelve words at most.</summary>
    private const string _snippetColumn = "snippet(notes_fts, 1, '', '', '…', 12) AS snippet";

    private readonly Database _database;

    public NoteRepository(Database database)
    {
        _database = database;
    }

    // -- Reading -- //

    /// <summary>
    /// One page of the notes a query asks for: the ones pinned first, then in the order the query
    /// asks for; the trash, the last moved there first. The search text of the query is not looked
    /// at: a search is the other way of listing.
    /// </summary>
    public Page<NoteSummary> List(NoteQuery query)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        string filters = Filters(command, query);
        command.CommandText = $"SELECT COUNT(*) FROM notes n WHERE 1 = 1 {filters}";
        int total = Rows.Count(command.ExecuteScalar());

        string order = query.InTrash ? "n.deleted_at DESC, n.id DESC" : $"n.pinned DESC, {OrderOf(query.Order)}";
        command.CommandText = $"SELECT {_summaryColumns} FROM notes n WHERE 1 = 1 {filters} ORDER BY {order} LIMIT $limit OFFSET $offset";
        command.Parameters.AddWithValue("$limit", query.Limit);
        command.Parameters.AddWithValue("$offset", query.Offset);
        using SqliteDataReader reader = command.ExecuteReader();
        var notes = new List<NoteSummary>();
        while (reader.Read())
        {
            notes.Add(ToSummary(reader));
        }

        return new Page<NoteSummary>(notes, total);
    }

    /// <summary>
    /// One page of the notes where every word of the search text of a query is found, in the title
    /// or the text, best first; the filters of the query narrow it as they narrow the list.
    /// </summary>
    public Page<SearchResult> Search(NoteQuery query)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        string filters = Filters(command, query);
        command.Parameters.AddWithValue("$match", ToMatchExpression(query.Text ?? ""));
        const string source = "FROM notes_fts JOIN notes n ON n.id = notes_fts.rowid WHERE notes_fts MATCH $match";
        command.CommandText = $"SELECT COUNT(*) {source} {filters}";
        int total = Rows.Count(command.ExecuteScalar());

        command.CommandText = $"""
            SELECT {_summaryColumns}, {_snippetColumn}, bm25(notes_fts) AS relevance
            {source} {filters}
            ORDER BY relevance, n.id DESC
            LIMIT $limit OFFSET $offset
            """;
        command.Parameters.AddWithValue("$limit", query.Limit);
        command.Parameters.AddWithValue("$offset", query.Offset);
        using SqliteDataReader reader = command.ExecuteReader();
        var results = new List<SearchResult>();
        while (reader.Read())
        {
            results.Add(new SearchResult(ToSummary(reader), reader.GetString(9)));
        }

        return new Page<SearchResult>(results, total);
    }

    /// <summary>Every note whole but the ones in the trash, most recently changed first: what an export takes out.</summary>
    public IReadOnlyList<Note> ListAll()
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {_summaryColumns}, n.content FROM notes n WHERE n.deleted_at IS NULL ORDER BY n.updated_at DESC, n.id DESC";
        using SqliteDataReader reader = command.ExecuteReader();
        var notes = new List<Note>();
        while (reader.Read())
        {
            notes.Add(ToNote(reader));
        }

        return notes;
    }

    /// <summary>
    /// The numbers of the notes in the trash, every one of them or the ones moved there before a
    /// moment: what is deleted for good when the trash is emptied, or when they have been there too long.
    /// </summary>
    public IReadOnlyList<long> ListTrashedIds(DateTimeOffset? deletedBefore = null)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM notes WHERE deleted_at IS NOT NULL AND ($before IS NULL OR deleted_at < $before) ORDER BY id";
        command.Parameters.AddWithValue("$before", deletedBefore is DateTimeOffset before ? Rows.Text(before) : DBNull.Value);
        using SqliteDataReader reader = command.ExecuteReader();
        var ids = new List<long>();
        while (reader.Read())
        {
            ids.Add(reader.GetInt64(0));
        }

        return ids;
    }

    /// <summary>One note whole, in the trash or not, or nothing when there is none with that number.</summary>
    public Note? Find(long id)
    {
        using SqliteConnection connection = _database.Connect();
        return Find(connection, id);
    }

    // -- Writing -- //

    /// <summary>Adds a note with its tags, in one step.</summary>
    /// <param name="createdAt">When it was made, for a note brought in from elsewhere; now when nothing is given.</param>
    /// <param name="updatedAt">When it last changed, for a note brought in from elsewhere; now when nothing is given.</param>
    public Note Create(string title, string content, long? folderId, IReadOnlyList<string> tags, DateTimeOffset? createdAt = null, DateTimeOffset? updatedAt = null)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteTransaction transaction = connection.BeginTransaction();
        long id;
        using (SqliteCommand command = connection.CreateCommand())
        {
            string now = Rows.Now();
            command.CommandText = """
                INSERT INTO notes (folder_id, title, content, created_at, updated_at)
                VALUES ($folderId, $title, $content, $createdAt, $updatedAt)
                RETURNING id
                """;
            command.Parameters.AddWithValue("$folderId", (object?)folderId ?? DBNull.Value);
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$content", content);
            command.Parameters.AddWithValue("$createdAt", createdAt is DateTimeOffset created ? Rows.Text(created) : now);
            command.Parameters.AddWithValue("$updatedAt", updatedAt is DateTimeOffset updated ? Rows.Text(updated) : now);
            id = (long)command.ExecuteScalar()!;
        }

        WriteTags(connection, id, tags);
        transaction.Commit();
        return Find(connection, id)!;
    }

    /// <summary>
    /// Saves the title and the text of a note, when it is still at the version they were based
    /// on; the version goes up by one.
    /// </summary>
    /// <returns>The note as saved, or nothing when it is no longer at that version, or is gone.</returns>
    public Note? Update(long id, int version, string title, string content)
    {
        using SqliteConnection connection = _database.Connect();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                UPDATE notes
                SET title = $title, content = $content, updated_at = $updatedAt, version = version + 1
                WHERE id = $id AND version = $version
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$version", version);
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$content", content);
            command.Parameters.AddWithValue("$updatedAt", Rows.Now());
            if (command.ExecuteNonQuery() == 0)
            {
                return null;
            }
        }

        return Find(connection, id);
    }

    /// <summary>
    /// Files a note in a folder, or in none. Filing is not editing: neither the version nor the
    /// date of the last change moves.
    /// </summary>
    /// <returns>Whether there was a note to file.</returns>
    public bool SetFolder(long id, long? folderId)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE notes SET folder_id = $folderId WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$folderId", (object?)folderId ?? DBNull.Value);
        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Puts a list of tags in place of the ones a note carries, and deletes the tags no note
    /// carries any longer, in one step. Neither the version nor the date of the last change moves.
    /// </summary>
    public void SetTags(long id, IReadOnlyList<string> tags)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteTransaction transaction = connection.BeginTransaction();
        WriteTags(connection, id, tags);
        RemoveUnusedTags(connection);
        transaction.Commit();
    }

    /// <summary>
    /// Puts another text in place of the one of a note being brought in, once the files it points at
    /// are attached to it and its addresses rewritten: it is still being made, so neither the
    /// version nor the date of the last change moves.
    /// </summary>
    public void Rewrite(long id, string content)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE notes SET content = $content WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$content", content);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Pins a note, so that it comes first in the list, or unpins it. Pinning is not editing:
    /// neither the version nor the date of the last change moves.
    /// </summary>
    /// <returns>Whether there was a note to pin.</returns>
    public bool SetPinned(long id, bool pinned)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE notes SET pinned = $pinned WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$pinned", pinned ? 1 : 0);
        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Moves a note to the trash, or puts it back from there. Neither the version nor the date of
    /// the last change moves.
    /// </summary>
    /// <returns>Whether there was a note to move: one not in the trash, or one in it.</returns>
    public bool SetTrashed(long id, bool trashed)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = trashed
            ? "UPDATE notes SET deleted_at = $deletedAt WHERE id = $id AND deleted_at IS NULL"
            : "UPDATE notes SET deleted_at = NULL WHERE id = $id AND deleted_at IS NOT NULL";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$deletedAt", Rows.Now());
        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Deletes a note for good, with its tag links and the records of its attachments, and the tags no
    /// note carries any longer, in one step. Its attachment files are for the caller to delete.
    /// </summary>
    /// <returns>Whether there was a note to delete.</returns>
    public bool Remove(long id)
    {
        using SqliteConnection connection = _database.Connect();
        using SqliteTransaction transaction = connection.BeginTransaction();
        bool removed;
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM notes WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            removed = command.ExecuteNonQuery() > 0;
        }

        RemoveUnusedTags(connection);
        transaction.Commit();
        return removed;
    }

    // -- Helpers -- //

    /// <summary>
    /// Turns the text a person searches for into a query of the index that means "all of these
    /// words". Every word is quoted, so nothing typed is read as an operator; a null character
    /// is dropped, since the index takes it for the end of the query.
    /// </summary>
    private static string ToMatchExpression(string text)
    {
        IEnumerable<string> words = text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => $"\"{word.Replace("\0", "", StringComparison.Ordinal).Replace("\"", "\"\"", StringComparison.Ordinal)}\"");
        return string.Join(' ', words);
    }

    /// <summary>The columns a list is ordered by, for each order a query may ask for; the last number made comes first among equals.</summary>
    private static string OrderOf(string order) => order switch
    {
        "created" => "n.created_at DESC, n.id DESC",
        "title" => "n.title COLLATE NOCASE, n.id DESC",
        _ => "n.updated_at DESC, n.id DESC",
    };

    /// <summary>The conditions of the filters a query asks for, with their values added to the command.</summary>
    private static string Filters(SqliteCommand command, NoteQuery query)
    {
        var filters = new StringBuilder(query.InTrash ? " AND n.deleted_at IS NOT NULL" : " AND n.deleted_at IS NULL");
        if (query.WithoutFolder)
        {
            filters.Append(" AND n.folder_id IS NULL");
        }
        else if (query.FolderId is long folderId)
        {
            filters.Append(" AND n.folder_id = $folderId");
            command.Parameters.AddWithValue("$folderId", folderId);
        }

        if (query.Tag is not null)
        {
            filters.Append(" AND EXISTS (SELECT 1 FROM note_tags nt JOIN tags t ON t.id = nt.tag_id WHERE nt.note_id = n.id AND t.name = $tag)");
            command.Parameters.AddWithValue("$tag", query.Tag);
        }

        return filters.ToString();
    }

    private static Note? Find(SqliteConnection connection, long id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {_summaryColumns}, n.content FROM notes n WHERE n.id = $id";
        command.Parameters.AddWithValue("$id", id);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ToNote(reader) : null;
    }

    private static void WriteTags(SqliteConnection connection, long noteId, IReadOnlyList<string> tags)
    {
        using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.CommandText = "DELETE FROM note_tags WHERE note_id = $noteId";
            clear.Parameters.AddWithValue("$noteId", noteId);
            clear.ExecuteNonQuery();
        }

        foreach (string tag in tags)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO tags (name) VALUES ($name) ON CONFLICT (name) DO NOTHING;
                INSERT INTO note_tags (note_id, tag_id) SELECT $noteId, id FROM tags WHERE name = $name;
                """;
            command.Parameters.AddWithValue("$noteId", noteId);
            command.Parameters.AddWithValue("$name", tag);
            command.ExecuteNonQuery();
        }
    }

    private static void RemoveUnusedTags(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM tags WHERE id NOT IN (SELECT tag_id FROM note_tags)";
        command.ExecuteNonQuery();
    }

    private static NoteSummary ToSummary(SqliteDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetString(1),
            Rows.NullableLong(reader, 2),
            Rows.Names(reader, 6),
            reader.GetInt32(3),
            Rows.Date(reader, 4),
            Rows.Date(reader, 5),
            Rows.NullableDate(reader, 7),
            reader.GetInt64(8) == 1);

    private static Note ToNote(SqliteDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(9),
            Rows.NullableLong(reader, 2),
            Rows.Names(reader, 6),
            reader.GetInt32(3),
            Rows.Date(reader, 4),
            Rows.Date(reader, 5),
            Rows.NullableDate(reader, 7),
            reader.GetInt64(8) == 1);
}
