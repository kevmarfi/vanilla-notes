using System.Globalization;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Storage;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>A backup of all the data, made while it is in use, and put back in place of the data there is; and the backup of the day.</summary>
public sealed class BackupServiceTests : IDisposable
{
    private readonly TestApplication _app = new();

    public void Dispose() => _app.Dispose();

    // -- The backup of the day -- //

    [Fact]
    public void TheBackupOfTheDayIsMadeWhenThereIsNone()
    {
        BackupInfo? made = _app.Backups.CreateAutomaticIfDue(open: null);

        Assert.NotNull(made);
        Assert.True(made.IsAutomatic);
        Assert.Equal([made.Name], _app.Backups.List().Select(backup => backup.Name));
    }

    [Fact]
    public void TheBackupOfTheDayIsNotMadeTwiceInADay()
    {
        _app.Backups.CreateAutomaticIfDue(open: null);

        Assert.Null(_app.Backups.CreateAutomaticIfDue(open: null));
        Assert.Single(_app.Backups.List());
    }

    [Fact]
    public void TheBackupOfTheDayIsMadeOnceTheLastOneIsADayOld()
    {
        MadeDaysAgo(_app.Backups.CreateAutomaticIfDue(open: null)!, 2);

        Assert.NotNull(_app.Backups.CreateAutomaticIfDue(open: null));
        Assert.Equal(2, _app.Backups.List().Count);
    }

    [Fact]
    public void ABackupThePersonMadeDoesNotStandForTheBackupOfTheDay()
    {
        _app.Backups.Create(open: null);

        Assert.NotNull(_app.Backups.CreateAutomaticIfDue(open: null));
    }

    [Fact]
    public void OnlyTheSevenNewestBackupsOfTheDayAreKeptAndNoneThePersonMade()
    {
        BackupInfo mine = MadeDaysAgo(_app.Backups.Create(open: null), 30);
        var older = new List<string>();
        for (int days = 20; days > 12; days--)
        {
            older.Add(MadeDaysAgo(_app.Backups.CreateAutomaticIfDue(open: null)!, days).Name);
        }

        BackupInfo newest = _app.Backups.CreateAutomaticIfDue(open: null)!;

        IReadOnlyList<BackupInfo> left = _app.Backups.List();
        Assert.Equal(BackupService.AutomaticKept, left.Count(backup => backup.IsAutomatic));
        Assert.Contains(left, backup => backup.Name == mine.Name);
        Assert.Contains(left, backup => backup.Name == newest.Name);
        Assert.DoesNotContain(left, backup => backup.Name == older[0] || backup.Name == older[1]);
    }

    /// <summary>Moves a backup back in time, as if it had been made days ago, by the name of its folder.</summary>
    private BackupInfo MadeDaysAgo(BackupInfo backup, int days)
    {
        string suffix = backup.IsAutomatic ? "-auto" : "";
        string name = DateTimeOffset.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH-mm-ss-fffZ", CultureInfo.InvariantCulture) + suffix;
        Directory.Move(_app.DataFolder.BackupFolder(backup.Name), _app.DataFolder.BackupFolder(name));
        return backup with { Name = name };
    }

    [Fact]
    public void ARestoredBackupBringsBackEveryProfileNoteAndFile()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile("Ana", TestApplication.Password);
        long noteId = ana.Notes.Create("Groceries", "Bread").Id;
        long fileId = ana.Attachments.Add(noteId, "list.txt", "text/plain", [1, 2, 3]).Id;
        TestApplication.OpenProfile zoe = _app.CreateProfile("Zoe");
        zoe.Notes.Create("Plans");
        zoe.Session.Dispose();

        BackupInfo backup = _app.Backups.Create(ana.Session);
        ana.Notes.Remove(noteId);
        ana.Session.Dispose();
        _app.ProfileService.Create("Bea");

        _app.Backups.Restore(backup.Name);

        Assert.Equal(["Ana", "Zoe"], _app.ProfileService.List().Select(profile => profile.Name));
        TestApplication.OpenProfile restored = _app.Open(ana.Id, TestApplication.Password);
        Assert.Equal("Bread", restored.Notes.Get(noteId).Content);
        Assert.Equal([1, 2, 3], restored.Attachments.ReadContent(fileId));
        Assert.Equal(1, _app.Open(zoe.Id).Notes.List(new NoteQuery()).Total);
    }

    [Fact]
    public void TheBackupOfAProfileWithAPasswordStaysEncrypted()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile("Ana", TestApplication.Password);
        ana.Notes.Create("Groceries for the weekend");

        BackupInfo backup = _app.Backups.Create(ana.Session);

        Assert.False(TestApplication.DiskHolds(_app.DataFolder.BackupFolder(backup.Name), "Groceries"));
    }

    [Fact]
    public void ARestoreIsUndoneWithTheBackupItMakesFirst()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile("Ana");
        ana.Session.Dispose();
        BackupInfo empty = _app.Backups.Create(open: null);
        TestApplication.OpenProfile zoe = _app.CreateProfile("Zoe");
        zoe.Session.Dispose();

        BackupInfo before = _app.Backups.Restore(empty.Name);
        Assert.Equal(["Ana"], _app.ProfileService.List().Select(profile => profile.Name));

        _app.Backups.Restore(before.Name);
        Assert.Equal(["Ana", "Zoe"], _app.ProfileService.List().Select(profile => profile.Name));
    }

    [Fact]
    public void BackupsAreListedNewestFirstAndDeleted()
    {
        BackupInfo first = _app.Backups.Create(open: null);
        Thread.Sleep(5);
        BackupInfo second = _app.Backups.Create(open: null);

        Assert.Equal([second.Name, first.Name], _app.Backups.List().Select(backup => backup.Name));

        _app.Backups.Delete(first.Name);
        Assert.Equal([second.Name], _app.Backups.List().Select(backup => backup.Name));
    }

    [Fact]
    public void ABackupThatDoesNotExistIsNotFound()
    {
        Assert.Throws<NotFoundException>(() => _app.Backups.Restore("2000-01-01T00-00-00-000Z"));
        Assert.Throws<NotFoundException>(() => _app.Backups.Delete("2000-01-01T00-00-00-000Z"));
    }

    [Fact]
    public void ABackupMovedElsewhereIsCopiedInAndRestoredOntoACleanDataFolder()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile("Ana", TestApplication.Password);
        long noteId = ana.Notes.Create("Groceries", "Bread").Id;
        BackupInfo backup = _app.Backups.Create(ana.Session);
        ana.Session.Dispose();
        DirectoryInfo elsewhere = Directory.CreateTempSubdirectory("vanilla-notes-moved-");
        string moved = Path.Combine(elsewhere.FullName, backup.Name);
        Directory.Move(_app.DataFolder.BackupFolder(backup.Name), moved);
        try
        {
            using var clean = new TestApplication();

            clean.Backups.RestoreFolder(moved);

            Assert.Equal(["Ana"], clean.ProfileService.List().Select(profile => profile.Name));
            Assert.Equal("Bread", clean.Open(ana.Id, TestApplication.Password).Notes.Get(noteId).Content);
            Assert.Contains(backup.Name, clean.DataFolder.ListBackups());
        }
        finally
        {
            elsewhere.Delete(recursive: true);
        }
    }

    [Fact]
    public void AFolderAmongTheBackupsIsRestoredAsItIs()
    {
        _app.CreateProfile("Ana").Session.Dispose();
        BackupInfo backup = _app.Backups.Create(open: null);
        _app.CreateProfile("Zoe").Session.Dispose();

        _app.Backups.RestoreFolder(_app.DataFolder.BackupFolder(backup.Name));

        Assert.Equal(["Ana"], _app.ProfileService.List().Select(profile => profile.Name));
        Assert.Equal(2, _app.Backups.List().Count);
    }

    [Fact]
    public void AFolderThatIsNotABackupIsRefusedAndNothingChanges()
    {
        _app.CreateProfile("Ana").Session.Dispose();
        DirectoryInfo folder = Directory.CreateTempSubdirectory("vanilla-notes-not-a-backup-");
        try
        {
            Assert.Equal("backup", Assert.Throws<ValidationException>(() => _app.Backups.RestoreFolder(folder.FullName)).Field);
            Assert.Equal(["Ana"], _app.ProfileService.List().Select(profile => profile.Name));
            Assert.Empty(_app.Backups.List());
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    // -- Cut short -- //

    [Fact]
    public void ARestoreCutShortAfterItsCopyIsFinishedAtTheNextStart()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile("Ana");
        long noteId = ana.Notes.Create("Groceries", "Bread").Id;
        ana.Session.Dispose();
        BackupInfo backup = _app.Backups.Create(open: null);
        _app.CreateProfile("Zoe").Session.Dispose();
        _app.Catalog.CloseConnections();

        // The application stops once the copy is complete, with the data it replaces half gone.
        _app.DataFolder.StageRestore(backup.Name);
        File.Delete(_app.DataFolder.CatalogPath);
        Directory.Delete(_app.DataFolder.ProfileFolder(ana.Id), recursive: true);

        _app.DataFolder.Prepare();

        Assert.Equal(["Ana"], _app.ProfileService.List().Select(profile => profile.Name));
        Assert.Equal("Bread", _app.Open(ana.Id).Notes.Get(noteId).Content);
    }

    [Fact]
    public void ARestoreCutShortDuringItsCopyLeavesTheDataAsItWas()
    {
        _app.CreateProfile("Ana").Session.Dispose();
        BackupInfo backup = _app.Backups.Create(open: null);
        _app.CreateProfile("Zoe").Session.Dispose();
        _app.Catalog.CloseConnections();

        // The application stops before the copy is complete, so the copy keeps its partial name.
        _app.DataFolder.StageRestore(backup.Name);
        string staged = Path.Combine(_app.DataFolder.Root, "restoring");
        Directory.Move(staged, staged + DataFolder.PartialSuffix);

        _app.DataFolder.Prepare();

        Assert.Equal(["Ana", "Zoe"], _app.ProfileService.List().Select(profile => profile.Name));
        Assert.False(Directory.Exists(staged + DataFolder.PartialSuffix));
    }

    [Fact]
    public void ABackupCutShortIsDeletedAtTheNextStart()
    {
        string partial = _app.DataFolder.StartBackup("2000-01-01T00-00-00-000Z");
        File.WriteAllBytes(Path.Combine(partial, "profiles.db"), [1, 2, 3]);

        _app.DataFolder.Prepare();

        Assert.False(Directory.Exists(partial));
        Assert.Empty(_app.Backups.List());
    }
}
