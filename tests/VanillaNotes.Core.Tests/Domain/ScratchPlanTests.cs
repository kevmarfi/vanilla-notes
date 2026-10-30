using Microsoft.Data.Sqlite;
using System.Diagnostics;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

public sealed class ScratchPlanTests : IClassFixture<LargeProfile>
{
    private readonly LargeProfile _large;
    public ScratchPlanTests(LargeProfile large) { _large = large; }

    [Fact]
    public void Plan()
    {
        using SqliteConnection c = _large.Profile.Session.Database.Connect();
        foreach (string sql in new[] {
            "EXPLAIN QUERY PLAN SELECT COUNT(*) FROM notes n WHERE 1 = 1  AND n.deleted_at IS NULL",
            "EXPLAIN QUERY PLAN SELECT n.id FROM notes n WHERE 1 = 1  AND n.deleted_at IS NULL ORDER BY n.pinned DESC, n.updated_at DESC, n.id DESC LIMIT 50" })
        {
            using var cmd = c.CreateCommand(); cmd.CommandText = sql;
            using var r = cmd.ExecuteReader();
            while (r.Read()) TestContext.Current.TestOutputHelper!.WriteLine(r.GetString(3));
        }
        foreach (string sql in new[] {"SELECT COUNT(*) FROM notes n WHERE 1 = 1  AND n.deleted_at IS NULL","SELECT n.id FROM notes n WHERE 1 = 1  AND n.deleted_at IS NULL ORDER BY n.pinned DESC, n.updated_at DESC, n.id DESC LIMIT 50"})
        {
            var w = Stopwatch.StartNew();
            using var cmd = c.CreateCommand(); cmd.CommandText = sql; using var r = cmd.ExecuteReader(); while (r.Read()) { }
            TestContext.Current.TestOutputHelper!.WriteLine($"{w.Elapsed.TotalMilliseconds}");
        }
    }
}
