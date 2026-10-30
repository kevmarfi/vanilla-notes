using System.Diagnostics;
using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Export;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>
/// How long the application takes with ten thousand notes in a profile with a password, the
/// heaviest case: each piece of work against a limit it must stay under. A measure is taken a few
/// times and the middle one kept, so that one slow run of the machine does not decide it. The
/// limits are about three times what was measured on the machine of the owner, to leave room for
/// the slower machines of the continuous integration; each measure is written to the output of the
/// test, to be compared over time.
/// </summary>
public sealed class LargeProfileTests : IClassFixture<LargeProfile>
{
    /// <summary>How many times a measure is taken, of which the middle one is kept.</summary>
    private const int _runs = 5;

    /// <summary>The longest bringing in ten thousand notes from a data file may take.</summary>
    private static readonly TimeSpan _importLimit = TimeSpan.FromSeconds(45);

    /// <summary>The longest opening the profile may take, the stretching of the password included.</summary>
    private static readonly TimeSpan _openLimit = TimeSpan.FromSeconds(2);

    /// <summary>The longest showing a page of the list may take: what the person waits for on every click of a folder.</summary>
    private static readonly TimeSpan _listLimit = TimeSpan.FromMilliseconds(400);

    /// <summary>The longest a search may take: it runs as the person types.</summary>
    private static readonly TimeSpan _searchLimit = TimeSpan.FromMilliseconds(300);

    /// <summary>The longest saving a note may take: it happens while the person writes.</summary>
    private static readonly TimeSpan _saveLimit = TimeSpan.FromMilliseconds(50);

    /// <summary>The longest a backup of all the data may take.</summary>
    private static readonly TimeSpan _backupLimit = TimeSpan.FromSeconds(5);

    /// <summary>The longest an export of every note to a data file may take.</summary>
    private static readonly TimeSpan _exportLimit = TimeSpan.FromSeconds(5);

    private readonly LargeProfile _large;

    public LargeProfileTests(LargeProfile large)
    {
        _large = large;
    }

    [Fact]
    public void TenThousandNotesAreBroughtInUnderTheLimit() => Within(_importLimit, _large.Importing);

    [Fact]
    public void TheProfileOpensUnderTheLimit() =>
        Within(_openLimit, Measure(() => _large.App.Open(_large.Profile.Id, TestApplication.Password).Session.Dispose()));

    [Fact]
    public void APageOfTheListIsShownUnderTheLimit()
    {
        Page<NoteSummary>? page = null;

        Within(_listLimit, Measure(() => page = _large.Profile.Notes.List(new NoteQuery())));

        Assert.Equal(LargeProfile.NoteCount, page!.Total);
    }

    [Fact]
    public void ASearchIsAnsweredUnderTheLimit()
    {
        Page<SearchResult>? page = null;

        Within(_searchLimit, Measure(() => page = _large.Profile.Notes.Search(new NoteQuery(Text: LargeProfile.RareWord))));

        Assert.Equal(LargeProfile.NoteCount / 10, page!.Total);
    }

    [Fact]
    public void ANoteIsSavedUnderTheLimit()
    {
        NoteSummary first = _large.Profile.Notes.List(new NoteQuery(Limit: 1)).Items[0];
        Note note = _large.Profile.Notes.Get(first.Id);

        Within(_saveLimit, Measure(() => note = _large.Profile.Notes.Edit(note.Id, note.Version, note.Title, note.Content + " more")));
    }

    [Fact]
    public void ABackupIsMadeUnderTheLimit() =>
        Within(_backupLimit, Measure(() => _large.App.Backups.Delete(_large.App.Backups.Create(_large.Profile.Session).Name)));

    [Fact]
    public void EveryNoteIsExportedUnderTheLimit()
    {
        var export = new ExportService(
            new NoteRepository(_large.Profile.Session.Database),
            new FolderRepository(_large.Profile.Session.Database),
            new AttachmentRepository(_large.Profile.Session.Database),
            _large.Profile.Session);

        Within(_exportLimit, Measure(() => export.Export(ExportFormat.Data)));
    }

    /// <summary>Does a piece of work a few times, and gives the middle of the times it took.</summary>
    private static TimeSpan Measure(Action work)
    {
        var times = new List<TimeSpan>(_runs);
        for (int run = 0; run < _runs; run++)
        {
            var watch = Stopwatch.StartNew();
            work();
            times.Add(watch.Elapsed);
        }

        return times.Order().ElementAt(_runs / 2);
    }

    /// <summary>Writes a measure to the output of the test, and fails the test when it is over its limit.</summary>
    private static void Within(TimeSpan limit, TimeSpan measured)
    {
        TestContext.Current.TestOutputHelper?.WriteLine($"{measured.TotalMilliseconds:F1} ms (limit {limit.TotalMilliseconds:F0} ms)");
        Assert.True(measured <= limit, $"Took {measured.TotalMilliseconds:F0} ms, over the limit of {limit.TotalMilliseconds:F0} ms.");
    }
}
