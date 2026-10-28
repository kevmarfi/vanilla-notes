using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Xunit;
using VanillaNotes.Core.Data;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Security;

namespace VanillaNotes.Core.Tests.Data;

/// <summary>
/// A database in a file, opened with its key or as a plain file, encrypted or decrypted in place
/// with the notes it already has, and refused when its file is damaged.
/// </summary>
public sealed class DatabaseTests : IDisposable
{
    /// <summary>A title looked for in the bytes of the file, to tell whether the file can be read without the key.</summary>
    private const string _title = "Groceries for the weekend";

    /// <summary>The size of a page of the file, the default of the engine, which the migrations do not change.</summary>
    private const int _pageSize = 4096;

    /// <summary>How many notes are written to make a file of many pages, so that one in the middle can be damaged.</summary>
    private const int _manyNotes = 200;

    /// <summary>The folder of the test, removed after it.</summary>
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("vanilla-notes-tests-");

    private string FilePath => Path.Combine(_folder.FullName, "notes.db");

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public void ADatabaseWithAKeyCannotBeReadWithoutIt()
    {
        byte[] key = KeyWrapper.CreateKey();
        WriteNote(Database.Open(FilePath, Migrator.Profile, key));

        Assert.False(FileHolds(_title));
        Assert.Throws<SqliteException>(() => Database.Open(FilePath, Migrator.Profile).Dispose());
        Assert.Throws<SqliteException>(() => Database.Open(FilePath, Migrator.Profile, KeyWrapper.CreateKey()).Dispose());
    }

    [Fact]
    public void ADatabaseWithAKeyOpensWithIt()
    {
        byte[] key = KeyWrapper.CreateKey();
        WriteNote(Database.Open(FilePath, Migrator.Profile, key));

        using Database database = Database.Open(FilePath, Migrator.Profile, key);

        Assert.Equal(1, CountNotes(database));
    }

    [Fact]
    public void ADatabaseWithoutAKeyIsAPlainFile()
    {
        WriteNote(Database.Open(FilePath, Migrator.Profile));

        Assert.True(FileHolds(_title));
    }

    [Fact]
    public void APlainDatabaseThatHasNotesIsEncryptedInPlace()
    {
        byte[] key = KeyWrapper.CreateKey();
        WriteNote(Database.Open(FilePath, Migrator.Profile));

        using (Database plain = Database.Open(FilePath, Migrator.Profile))
        {
            plain.ChangeKey(key);
        }

        Assert.False(FileHolds(_title));
        using Database database = Database.Open(FilePath, Migrator.Profile, key);
        Assert.Equal(1, CountNotes(database));
    }

    [Fact]
    public void AnEncryptedDatabaseThatHasNotesIsDecryptedInPlace()
    {
        byte[] key = KeyWrapper.CreateKey();
        WriteNote(Database.Open(FilePath, Migrator.Profile, key));

        using (Database encrypted = Database.Open(FilePath, Migrator.Profile, key))
        {
            encrypted.ChangeKey([]);
        }

        Assert.True(FileHolds(_title));
        using Database database = Database.Open(FilePath, Migrator.Profile);
        Assert.Equal(1, CountNotes(database));
    }

    [Fact]
    public void ADamagedFileIsRefused()
    {
        WriteManyNotes(Database.Open(FilePath, Migrator.Profile));
        DamageMiddlePage();

        var failure = Assert.Throws<StorageException>(() => Database.Open(FilePath, Migrator.Profile).Dispose());

        Assert.Equal(ErrorCodes.DatabaseDamaged, failure.Code);
    }

    [Fact]
    public void ADamagedEncryptedFileIsRefused()
    {
        byte[] key = KeyWrapper.CreateKey();
        WriteManyNotes(Database.Open(FilePath, Migrator.Profile, key));
        DamageMiddlePage();

        var failure = Assert.Throws<StorageException>(() => Database.Open(FilePath, Migrator.Profile, key).Dispose());

        Assert.Equal(ErrorCodes.DatabaseDamaged, failure.Code);
    }

    [Fact]
    public void EveryConnectionEnforcesForeignKeys()
    {
        using Database database = Database.Open(FilePath, Migrator.Profile);
        using SqliteConnection connection = database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys";

        Assert.Equal(1L, command.ExecuteScalar());
    }

    /// <summary>Writes one note and lets go of the database, so that its file can be read.</summary>
    private static void WriteNote(Database database)
    {
        using (database)
        using (SqliteConnection connection = database.Connect())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO notes (title, content) VALUES ($title, 'Bread, milk, and coffee.')";
            command.Parameters.AddWithValue("$title", _title);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>Writes enough notes to fill many pages, and lets go of the database, so that its file can be changed.</summary>
    private static void WriteManyNotes(Database database)
    {
        using (database)
        using (SqliteConnection connection = database.Connect())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO notes (title, content) VALUES ($title, $content)";
            SqliteParameter title = command.Parameters.Add("$title", SqliteType.Text);
            command.Parameters.AddWithValue("$content", new string('x', _pageSize / 2));
            for (int index = 0; index < _manyNotes; index++)
            {
                title.Value = string.Create(CultureInfo.InvariantCulture, $"Note {index}");
                command.ExecuteNonQuery();
            }
        }
    }

    /// <summary>Writes over a page in the middle of the file, as a failing disk would.</summary>
    private void DamageMiddlePage()
    {
        using FileStream file = File.Open(FilePath, FileMode.Open, FileAccess.ReadWrite);
        long pages = file.Length / _pageSize;
        file.Position = pages / 2 * _pageSize;
        byte[] garbage = new byte[_pageSize];
        Array.Fill(garbage, (byte)0xA5);
        file.Write(garbage);
    }

    private static long CountNotes(Database database)
    {
        using SqliteConnection connection = database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM notes";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Whether the text is in the bytes of the file and of its write-ahead log, as anyone could read them.</summary>
    private bool FileHolds(string text)
    {
        byte[] wanted = Encoding.UTF8.GetBytes(text);
        return new[] { FilePath, FilePath + "-wal" }
            .Where(File.Exists)
            .Any(path => File.ReadAllBytes(path).AsSpan().IndexOf(wanted) >= 0);
    }
}
