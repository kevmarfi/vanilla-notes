using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace VanillaNotes.Core.Data.Repositories;

/// <summary>
/// How every repository reads and writes the values that are not plain text or numbers: dates,
/// kept as text in UTC, and the tags of a note, gathered as a JSON list by the query.
/// </summary>
internal static class Rows
{
    /// <summary>The form a date is kept in: ISO 8601, in UTC, to the millisecond, as the web version keeps it.</summary>
    private const string _dateFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    /// <summary>The extended error of the engine for a value that breaks a unique constraint.</summary>
    private const int _uniqueViolation = 2067;

    /// <summary>The moment now, in the form a date is kept in.</summary>
    public static string Now() => Text(DateTimeOffset.UtcNow);

    /// <summary>A moment in the form a date is kept in.</summary>
    public static string Text(DateTimeOffset moment) => moment.UtcDateTime.ToString(_dateFormat, CultureInfo.InvariantCulture);

    /// <summary>A date read from a column.</summary>
    public static DateTimeOffset Date(SqliteDataReader reader, int ordinal) =>
        DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    /// <summary>A date that may be missing, read from a column.</summary>
    public static DateTimeOffset? NullableDate(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Date(reader, ordinal);

    /// <summary>A number that may be missing, read from a column.</summary>
    public static long? NullableLong(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);

    /// <summary>A text that may be missing, read from a column.</summary>
    public static string? NullableString(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    /// <summary>A list of names the query gathered as a JSON list.</summary>
    public static IReadOnlyList<string> Names(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? [] : JsonSerializer.Deserialize<List<string>>(reader.GetString(ordinal)) ?? [];

    /// <summary>Whether a failure of the engine is a value that is already taken where it must be unique.</summary>
    public static bool IsUniqueViolation(SqliteException exception) => exception.SqliteExtendedErrorCode == _uniqueViolation;

    /// <summary>A number read as the engine gives it back from a count or a sum.</summary>
    public static int Count(object? value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);
}
