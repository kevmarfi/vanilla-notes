using Microsoft.Data.Sqlite;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Data.Repositories;

/// <summary>
/// The profiles of this machine, in the catalog: their names, and the key of each one that has a
/// password, wrapped by the password, by the recovery code, and by Windows Hello when it is on. It is the only repository that
/// reads the catalog; the others read the database of the profile that is open.
/// </summary>
public sealed class ProfileRepository
{
    private const string _columns = "id, name, password_key IS NOT NULL AS has_password, hello_key IS NOT NULL AS has_hello, created_at";

    private readonly Database _catalog;

    public ProfileRepository(Database catalog)
    {
        _catalog = catalog;
    }

    // -- Reading -- //

    /// <summary>Every profile, in the order of their names.</summary>
    public IReadOnlyList<Profile> List()
    {
        using SqliteConnection connection = _catalog.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {_columns} FROM profiles ORDER BY name, id";
        using SqliteDataReader reader = command.ExecuteReader();
        var profiles = new List<Profile>();
        while (reader.Read())
        {
            profiles.Add(ToProfile(reader));
        }

        return profiles;
    }

    /// <summary>One profile, or nothing when there is none with that number.</summary>
    public Profile? Find(long id)
    {
        using SqliteConnection connection = _catalog.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {_columns} FROM profiles WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ToProfile(reader) : null;
    }

    /// <summary>The key of a profile wrapped by its password, or nothing when it has no password.</summary>
    public string? FindPasswordKey(long id) => FindKey(id, "password_key");

    /// <summary>The key of a profile wrapped by its recovery code, or nothing when it has no password.</summary>
    public string? FindRecoveryKey(long id) => FindKey(id, "recovery_key");

    /// <summary>The key of a profile wrapped by Windows Hello, or nothing when Hello is not on for it.</summary>
    public string? FindHelloKey(long id) => FindKey(id, "hello_key");

    // -- Writing -- //

    /// <summary>
    /// Adds a profile with no password.
    /// </summary>
    /// <returns>The profile, or nothing when another one already has that name.</returns>
    public Profile? Create(string name)
    {
        long id;
        try
        {
            using SqliteConnection connection = _catalog.Connect();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO profiles (name, created_at) VALUES ($name, $createdAt) RETURNING id";
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
    /// Gives a profile another name.
    /// </summary>
    /// <returns>The profile, or nothing when another one already has that name.</returns>
    public Profile? Rename(long id, string name)
    {
        try
        {
            using SqliteConnection connection = _catalog.Connect();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE profiles SET name = $name WHERE id = $id";
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
    /// Keeps the key of a profile wrapped by its password and by its recovery code, or forgets
    /// both when the profile no longer has a password.
    /// </summary>
    public void SetKeys(long id, string? passwordKey, string? recoveryKey)
    {
        using SqliteConnection connection = _catalog.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE profiles SET password_key = $passwordKey, recovery_key = $recoveryKey WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$passwordKey", (object?)passwordKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$recoveryKey", (object?)recoveryKey ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Keeps the key of a profile wrapped by Windows Hello, or forgets it when Hello is turned off.
    /// </summary>
    public void SetHelloKey(long id, string? helloKey)
    {
        using SqliteConnection connection = _catalog.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE profiles SET hello_key = $helloKey WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$helloKey", (object?)helloKey ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Takes a profile off the list.
    /// </summary>
    /// <returns>Whether there was one to take off.</returns>
    public bool Remove(long id)
    {
        using SqliteConnection connection = _catalog.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM profiles WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() > 0;
    }

    private string? FindKey(long id, string column)
    {
        using SqliteConnection connection = _catalog.Connect();
        using SqliteCommand command = connection.CreateCommand();

        // The column is one of three names written in this class, never a value from outside.
        command.CommandText = $"SELECT {column} FROM profiles WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() as string;
    }

    private static Profile ToProfile(SqliteDataReader reader) =>
        new(reader.GetInt64(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3), Rows.Date(reader, 4));
}
