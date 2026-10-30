using System.Globalization;
using Microsoft.Data.Sqlite;
using Xunit;
using VanillaNotes.Core.Data;
using VanillaNotes.Core.Errors;

namespace VanillaNotes.Core.Tests.Data;

/// <summary>A database brought up to the schema of this release, and refused when it is ahead of it.</summary>
public sealed class MigratorTests
{
    [Fact]
    public void TheCatalogReceivesTheListOfTheProfiles()
    {
        using Database database = Database.OpenInMemory(Migrator.Catalog);
        using SqliteConnection connection = database.Connect();

        Assert.Equal(["profiles", "schema_migrations"], Tables(connection));
        Assert.Equal(1, Scalar(connection, "SELECT COUNT(*) FROM schema_migrations"));
    }

    [Fact]
    public void TheDatabaseOfAProfileReceivesEverythingItOwns()
    {
        using Database database = Database.OpenInMemory(Migrator.Profile);
        using SqliteConnection connection = database.Connect();

        Assert.Superset(
            new HashSet<string> { "attachments", "folders", "note_tags", "notes", "notes_fts", "schema_migrations", "settings", "tags", "workspace" },
            new HashSet<string>(Tables(connection)));
    }

    [Fact]
    public void TheSearchIndexFollowsTheNotes()
    {
        using Database database = Database.OpenInMemory(Migrator.Profile);
        using SqliteConnection connection = database.Connect();

        Execute(connection, "INSERT INTO notes (title, content) VALUES ('Café', 'A note about a café')");

        Assert.Equal(1, Scalar(connection, "SELECT COUNT(*) FROM notes_fts WHERE notes_fts MATCH '\"cafe\"'"));
    }

    [Fact]
    public void ApplyingAgainChangesNothing()
    {
        using Database database = Database.OpenInMemory(Migrator.Profile);
        using SqliteConnection connection = database.Connect();

        Migrator.Apply(connection, Migrator.Profile);

        Assert.Equal(1, Scalar(connection, "SELECT COUNT(*) FROM schema_migrations"));
    }

    [Fact]
    public void ForeignKeysAreEnforcedAgainAfterMigrating()
    {
        using Database database = Database.OpenInMemory(Migrator.Profile);
        using SqliteConnection connection = database.Connect();

        Migrator.Apply(connection, Migrator.Profile);

        Assert.Equal(1, Scalar(connection, "PRAGMA foreign_keys"));
    }

    [Fact]
    public void ADatabaseOfANewerReleaseIsRefused()
    {
        using Database database = Database.OpenInMemory(Migrator.Catalog);
        using SqliteConnection connection = database.Connect();
        Execute(connection, "INSERT INTO schema_migrations (version, name, applied_at) VALUES (99, '0099-future.sql', '2030-01-01T00:00:00.000Z')");

        var failure = Assert.Throws<StorageException>(() => Migrator.Apply(connection, Migrator.Catalog));

        Assert.Equal(ErrorCodes.DatabaseTooNew, failure.Code);
    }

    [Fact]
    public void ASetThatDoesNotExistIsAFaultOfTheRelease()
    {
        using Database database = Database.OpenInMemory(Migrator.Catalog);
        using SqliteConnection connection = database.Connect();

        Assert.Throws<InvalidOperationException>(() => Migrator.Apply(connection, "Nothing"));
    }

    private static List<string> Tables(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE 'notes_fts_%' ORDER BY name";
        using SqliteDataReader reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
