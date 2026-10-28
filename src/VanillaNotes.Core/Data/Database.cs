using Microsoft.Data.Sqlite;
using VanillaNotes.Core.Errors;

namespace VanillaNotes.Core.Data;

/// <summary>
/// One database of the application, the catalog of the profiles or the one of a profile, ready
/// to be used: opened with its key when it has one, set up, and with every migration of its kind
/// applied. A repository asks it for a connection for each piece of work and closes it after.
/// </summary>
public sealed class Database : IDisposable
{
    /// <summary>The error of the engine for a file it cannot read: encrypted with another key, or not encrypted when a key was given.</summary>
    private const int _notADatabase = 26;

    /// <summary>The error of the engine for a file whose contents are damaged.</summary>
    private const int _corrupt = 11;

    /// <summary>The one row the engine answers a check of the file with when it finds nothing damaged.</summary>
    private const string _intact = "ok";

    /// <summary>The text the engine takes a key in, before its hexadecimal digits, to use it as it is instead of stretching it as a passphrase.</summary>
    private const string _rawKeyPrefix = "raw:";

    /// <summary>What the driver is told to open: the file, and the key when there is one. It changes when the key does.</summary>
    private string _connectionString;

    /// <summary>A connection kept open for the whole life of a database held in memory, which is gone once its last connection closes.</summary>
    private readonly SqliteConnection? _memoryAnchor;

    private Database(string connectionString, SqliteConnection? memoryAnchor)
    {
        _connectionString = connectionString;
        _memoryAnchor = memoryAnchor;
    }

    /// <summary>
    /// Opens the database in a file, making it if it does not exist, checks that its file is not
    /// damaged, and applies the migrations of its kind it has not received. A database with a key
    /// is encrypted with it; one without is a plain file. A damaged file is refused before anything
    /// is written to it, so that it can be put back from a backup as it was.
    /// </summary>
    /// <param name="migrationSet">Which kind of database it is: the catalog or a profile, as the migrator names them.</param>
    /// <param name="key">The key of the profile, or nothing for a database that is not encrypted.</param>
    public static Database Open(string filePath, string migrationSet, ReadOnlySpan<byte> key = default)
    {
        var database = new Database(FileConnectionString(filePath, key), memoryAnchor: null);
        try
        {
            using SqliteConnection connection = database.Connect();
            WriteAhead(connection);
            CheckIntact(connection);
            Migrator.Apply(connection, migrationSet);
            return database;
        }
        catch (SqliteException exception) when ((exception.SqliteErrorCode & 0xFF) == _corrupt)
        {
            database.Dispose();
            throw Damaged();
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens the database in a file as the other way of opening does, when the file is the one
    /// the key opens: encrypted with that key, or plain when no key is given.
    /// </summary>
    /// <param name="migrationSet">Which kind of database it is: the catalog or a profile, as the migrator names them.</param>
    /// <param name="key">The key of the profile, or nothing for a database that is not encrypted.</param>
    /// <returns>The database, or nothing when the file is not the one the key opens.</returns>
    public static Database? OpenIfKeyFits(string filePath, string migrationSet, ReadOnlySpan<byte> key = default)
    {
        try
        {
            return Open(filePath, migrationSet, key);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == _notADatabase)
        {
            return null;
        }
    }

    /// <summary>
    /// Makes a database that lives in memory only, with every migration of its kind applied. It is
    /// what the tests run on, and it is gone once disposed.
    /// </summary>
    /// <param name="migrationSet">Which kind of database it is: the catalog or a profile, as the migrator names them.</param>
    public static Database OpenInMemory(string migrationSet)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Guid.NewGuid().ToString("N"),
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        };
        var anchor = new SqliteConnection(builder.ToString());
        anchor.Open();
        var database = new Database(builder.ToString(), anchor);
        try
        {
            using SqliteConnection connection = database.Connect();
            Migrator.Apply(connection, migrationSet);
            return database;
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens a connection to the database, with the settings every connection must have: foreign
    /// keys enforced, and writes that do not wait for the disk at every step.
    /// </summary>
    public SqliteConnection Connect()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA synchronous = NORMAL;";
            command.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Encrypts the database with a new key, or decrypts it when no key is given, in place and
    /// with everything it holds. It goes on being used as before, now with the key it has.
    /// </summary>
    /// <param name="newKey">The key it is encrypted with from now on, or nothing to leave it as a plain file.</param>
    public void ChangeKey(ReadOnlySpan<byte> newKey)
    {
        // The engine refuses to encrypt a plain file in write-ahead mode, and leaving that mode
        // needs the only connection to the file: the ones the driver keeps open are closed first.
        // The mode is taken back once the key has changed.
        CloseConnections();
        try
        {
            using SqliteConnection connection = Connect();
            using SqliteCommand command = connection.CreateCommand();

            // A pragma takes no parameter. The text is made here, from hexadecimal digits only.
            command.CommandText = $"PRAGMA journal_mode = DELETE; PRAGMA rekey = '{(newKey.IsEmpty ? "" : RawKey(newKey))}';";
            command.ExecuteNonQuery();
        }
        finally
        {
            CloseConnections();
        }

        var builder = new SqliteConnectionStringBuilder(_connectionString) { Password = newKey.IsEmpty ? "" : RawKey(newKey) };
        _connectionString = builder.ToString();
        using SqliteConnection reopened = Connect();
        WriteAhead(reopened);
    }

    /// <summary>
    /// Writes a complete and consistent copy of the database to a new file, while it goes on being
    /// used. The copy is encrypted with the same key as the database, or plain when it has none.
    /// </summary>
    public void CopyTo(string filePath)
    {
        using SqliteConnection connection = Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $path";
        command.Parameters.AddWithValue("$path", filePath);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Closes the connections the driver keeps open for the database, so that its file can be
    /// copied, moved, deleted, or replaced. The database can still be used: the next connection
    /// opens the file anew.
    /// </summary>
    public void CloseConnections()
    {
        using var connection = new SqliteConnection(_connectionString);
        SqliteConnection.ClearPool(connection);
    }

    /// <summary>
    /// Lets go of the database: its connections are closed, and a database held in memory is gone.
    /// </summary>
    public void Dispose()
    {
        CloseConnections();
        _memoryAnchor?.Dispose();
    }

    private static string RawKey(ReadOnlySpan<byte> key) => _rawKeyPrefix + Convert.ToHexString(key);

    private static string FileConnectionString(string filePath, ReadOnlySpan<byte> key)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = filePath, Pooling = true };
        if (!key.IsEmpty)
        {
            builder.Password = RawKey(key);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Has the engine read the whole file and refuses it when a page, a row, or an index of it is
    /// damaged. The quick check is taken over the full one: it finds the damage a crash or a disk
    /// leaves, in a fraction of the time, which is paid each time a profile is opened.
    /// </summary>
    private static void CheckIntact(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        if (command.ExecuteScalar() is not _intact)
        {
            throw Damaged();
        }
    }

    private static StorageException Damaged() =>
        new(ErrorCodes.DatabaseDamaged, "The file of the database is damaged.");

    /// <summary>Puts a database in a file in write-ahead mode, which lets a search read while a save writes.</summary>
    private static void WriteAhead(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL;";
        command.ExecuteNonQuery();
    }
}
