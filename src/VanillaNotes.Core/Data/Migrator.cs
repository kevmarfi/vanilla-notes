using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;
using VanillaNotes.Core.Errors;

namespace VanillaNotes.Core.Data;

/// <summary>
/// Brings a database up to the schema of this release, by applying, in order, the numbered
/// changes it has not received yet. There are two sets of them, one for the catalog of the
/// profiles and one for the database of a profile, carried inside the application.
/// </summary>
public static class Migrator
{
    /// <summary>The set of migrations of the catalog of the profiles.</summary>
    public const string Catalog = "Catalog";

    /// <summary>The set of migrations of the database of a profile.</summary>
    public const string Profile = "Profile";

    /// <summary>What the name of every migration carried in the application starts with, before its set and its file.</summary>
    private const string _resourcePrefix = "Migrations/";

    /// <summary>The record of what a database has received, created before the first migration runs.</summary>
    private const string _recordTable = """
        CREATE TABLE IF NOT EXISTS schema_migrations (
          version    INTEGER PRIMARY KEY,
          name       TEXT NOT NULL,
          applied_at TEXT NOT NULL
        ) STRICT;
        """;

    /// <summary>One schema change: its number, the name of its file, and its SQL.</summary>
    private sealed record Migration(int Version, string Name, string Sql);

    /// <summary>
    /// Applies to the database of a connection the migrations of a set it has not received, each
    /// in a transaction of its own. A migration that fails leaves the database as the previous one
    /// left it, and the failure goes on to the caller.
    /// </summary>
    /// <param name="set">Which set to apply: the catalog or a profile.</param>
    public static void Apply(SqliteConnection connection, string set)
    {
        IReadOnlyList<Migration> migrations = Read(set);
        Execute(connection, _recordTable);
        int current = CurrentVersion(connection);
        if (current > migrations.Count)
        {
            throw new StorageException(
                ErrorCodes.DatabaseTooNew,
                $"The database has received migration {current} of the set {set}, and this release knows {migrations.Count}.");
        }

        if (current == migrations.Count)
        {
            return;
        }

        // Foreign keys cannot be turned off inside a transaction: they are off for the whole run.
        Execute(connection, "PRAGMA foreign_keys = OFF;");
        try
        {
            foreach (Migration migration in migrations.Skip(current))
            {
                ApplyOne(connection, migration);
            }
        }
        finally
        {
            Execute(connection, "PRAGMA foreign_keys = ON;");
        }
    }

    // -- Applying -- //

    private static void ApplyOne(SqliteConnection connection, Migration migration)
    {
        using SqliteTransaction transaction = connection.BeginTransaction();
        Execute(connection, migration.Sql);

        using (SqliteCommand check = connection.CreateCommand())
        {
            check.CommandText = "PRAGMA foreign_key_check;";
            using SqliteDataReader reader = check.ExecuteReader();
            if (reader.Read())
            {
                throw new InvalidOperationException($"Migration {migration.Name} leaves rows that point at nothing in {reader.GetString(0)}.");
            }
        }

        using (SqliteCommand record = connection.CreateCommand())
        {
            record.CommandText = "INSERT INTO schema_migrations (version, name, applied_at) VALUES ($version, $name, $appliedAt)";
            record.Parameters.AddWithValue("$version", migration.Version);
            record.Parameters.AddWithValue("$name", migration.Name);
            record.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
            record.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static int CurrentVersion(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    // -- Reading the files -- //

    /// <summary>
    /// The migrations of a set, in order. Files that are misnumbered are a fault of the release,
    /// not of the database, and stop the application before it touches any.
    /// </summary>
    private static List<Migration> Read(string set)
    {
        Assembly assembly = typeof(Migrator).Assembly;
        string prefix = $"{_resourcePrefix}{set}/";
        // The folder of the set is joined to the file by the separator of the machine that built it.
        var migrations = assembly.GetManifestResourceNames()
            .Where(name => name.Replace('\\', '/').StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => ReadOne(assembly, name, name[prefix.Length..]))
            .OrderBy(migration => migration.Version)
            .ToList();

        if (migrations.Count == 0)
        {
            throw new InvalidOperationException($"There is no migration of the set {set}.");
        }

        for (int i = 0; i < migrations.Count; i++)
        {
            if (migrations[i].Version != i + 1)
            {
                throw new InvalidOperationException($"The migrations of the set {set} are not numbered from 1 with no gaps and no repeats: {migrations[i].Name}.");
            }
        }

        return migrations;
    }

    private static Migration ReadOne(Assembly assembly, string resource, string fileName)
    {
        if (fileName.Length < 6 || fileName[4] != '-' || !int.TryParse(fileName.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out int version))
        {
            throw new InvalidOperationException($"The migration {fileName} is not named NNNN-short-name.sql.");
        }

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return new Migration(version, fileName, reader.ReadToEnd());
    }
}
