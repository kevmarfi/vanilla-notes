using System.Globalization;
using Microsoft.Data.Sqlite;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>Writing, listing, filing, tagging, and deleting the notes of a profile, as the web version does, and the trash.</summary>
public sealed class NoteServiceTests : IDisposable
{
    private readonly TestApplication _app = new();

    private readonly TestApplication.OpenProfile _ana;

    public NoteServiceTests()
    {
        _ana = _app.CreateProfile();
    }

    public void Dispose() => _app.Dispose();

    // -- Writing -- //

    [Fact]
    public void ANoteIsCreatedReadEditedAndDeleted()
    {
        Note created = _ana.Notes.Create("Groceries", "Bread");

        Note edited = _ana.Notes.Edit(created.Id, created.Version, "Groceries", "Bread and milk");

        Assert.Equal("Bread and milk", _ana.Notes.Get(created.Id).Content);
        Assert.Equal(created.Version + 1, edited.Version);
        _ana.Notes.Remove(created.Id);
        Assert.Throws<NotFoundException>(() => _ana.Notes.Get(created.Id));
    }

    [Fact]
    public void TheTitleIsTrimmedAndTheContentIsEmptyByDefault()
    {
        Note note = _ana.Notes.Create("  Groceries  ", content: null);

        Assert.Equal("Groceries", note.Title);
        Assert.Equal("", note.Content);
        Assert.Equal(1, note.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ANoteNeedsATitle(string title)
    {
        Assert.Equal("title", Assert.Throws<ValidationException>(() => _ana.Notes.Create(title)).Field);
    }

    [Fact]
    public void ATitleOrAContentOverTheLimitIsRefused()
    {
        Assert.Equal("title", Assert.Throws<ValidationException>(() => _ana.Notes.Create(new string('a', 201))).Field);
        Assert.Equal("content", Assert.Throws<ValidationException>(() => _ana.Notes.Create("Long", new string('a', 100_001))).Field);
        _ana.Notes.Create(new string('a', 200), new string('a', 100_000));
    }

    [Fact]
    public void AnEditBasedOnAnOldVersionIsRefusedAndChangesNothing()
    {
        Note note = _ana.Notes.Create("Groceries", "Bread");
        _ana.Notes.Edit(note.Id, note.Version, "Groceries", "Bread and milk");

        var failure = Assert.Throws<ConflictException>(() => _ana.Notes.Edit(note.Id, note.Version, "Groceries", "Only bread"));

        Assert.Equal(ErrorCodes.VersionConflict, failure.Code);
        Assert.Equal("Bread and milk", _ana.Notes.Get(note.Id).Content);
    }

    [Fact]
    public void SavingWhatTheNoteAlreadySaysKeepsTheVersion()
    {
        Note note = _ana.Notes.Create("Groceries", "Bread");

        Note saved = _ana.Notes.Edit(note.Id, note.Version, " Groceries ", "Bread");

        Assert.Equal(note.Version, saved.Version);
        Assert.Equal(note.UpdatedAt, saved.UpdatedAt);
    }

    [Fact]
    public void AnInvalidEditChangesNothing()
    {
        Note note = _ana.Notes.Create("Groceries", "Bread");

        Assert.Throws<ValidationException>(() => _ana.Notes.Edit(note.Id, note.Version, "", "Bread and milk"));

        Note unchanged = _ana.Notes.Get(note.Id);
        Assert.Equal((note.Title, note.Content, note.Version), (unchanged.Title, unchanged.Content, unchanged.Version));
    }

    [Fact]
    public void ANoteThatDoesNotExistIsNotFound()
    {
        Assert.Throws<NotFoundException>(() => _ana.Notes.Get(999));
        Assert.Throws<NotFoundException>(() => _ana.Notes.Edit(999, 1, "Title", ""));
        Assert.Throws<NotFoundException>(() => _ana.Notes.Remove(999));
        Assert.Throws<NotFoundException>(() => _ana.Notes.Move(999, null));
    }

    [Fact]
    public void NothingNewIsStoredWhileTheDiskIsRunningOut()
    {
        using var app = new TestApplication(diskIsFull: true);
        TestApplication.OpenProfile bea = app.CreateProfile("Bea");

        Assert.Equal(ErrorCodes.StorageFull, Assert.Throws<StorageException>(() => bea.Notes.Create("A note")).Code);
    }

    // -- Filing and tagging -- //

    [Fact]
    public void ANoteIsFiledInAFolderAndTakenOutOfIt()
    {
        Folder work = _ana.Folders.Create("Work");
        Note note = _ana.Notes.Create("A note", folderId: work.Id);

        Assert.Equal(work.Id, note.FolderId);
        Assert.Null(_ana.Notes.Move(note.Id, null).FolderId);
    }

    [Fact]
    public void FilingOrTaggingIsNotEditing()
    {
        Folder work = _ana.Folders.Create("Work");
        Note note = _ana.Notes.Create("A note");

        _ana.Notes.Move(note.Id, work.Id);
        Note tagged = _ana.Notes.SetTags(note.Id, ["home"]);

        Assert.Equal(note.Version, tagged.Version);
        Assert.Equal(note.UpdatedAt, tagged.UpdatedAt);
    }

    [Fact]
    public void AFolderThatDoesNotExistIsRefusedAndNothingIsStored()
    {
        Note note = _ana.Notes.Create("A note");

        Assert.Equal("folderId", Assert.Throws<ValidationException>(() => _ana.Notes.Create("Another", folderId: 999)).Field);
        Assert.Equal("folderId", Assert.Throws<ValidationException>(() => _ana.Notes.Move(note.Id, 999)).Field);
        Assert.Equal(1, _ana.Notes.List(new NoteQuery()).Total);
    }

    [Fact]
    public void TagsAreKeptInLowercaseAndInOrder()
    {
        Note note = _ana.Notes.Create("A note", tags: ["Work", "ideas", "año_2026"]);

        Assert.Equal(["año_2026", "ideas", "work"], note.Tags);
    }

    [Fact]
    public void SettingTagsReplacesTheWholeList()
    {
        Note note = _ana.Notes.Create("A note", tags: ["work", "ideas"]);

        Assert.Equal(["home"], _ana.Notes.SetTags(note.Id, ["home"]).Tags);
    }

    [Theory]
    [InlineData("two words")]
    [InlineData("comma,separated")]
    [InlineData("")]
    [InlineData("a-tag-that-is-longer-than-thirty")]
    public void ATagOfTheWrongFormIsRefused(string tag)
    {
        Assert.Equal("tags", Assert.Throws<ValidationException>(() => _ana.Notes.Create("A note", tags: [tag])).Field);
    }

    [Fact]
    public void ANoteCarriesTenTagsAtMostAndNoneTwice()
    {
        Assert.Equal("tags", Assert.Throws<ValidationException>(() => _ana.Notes.Create("A note", tags: Enumerable.Range(1, 11).Select(i => $"t{i}"))).Field);
        Assert.Equal("tags", Assert.Throws<ValidationException>(() => _ana.Notes.Create("A note", tags: ["work", "WORK"])).Field);
        _ana.Notes.Create("A note", tags: Enumerable.Range(1, 10).Select(i => $"t{i}"));
    }

    // -- Listing -- //

    [Fact]
    public void TheListHoldsNoNotesForAnEmptyProfile()
    {
        Page<NoteSummary> page = _ana.Notes.List(new NoteQuery());

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public void TheMostRecentlyChangedNoteComesFirst()
    {
        Note first = _ana.Notes.Create("First");
        _ana.Notes.Create("Second");

        // Dates are kept to the millisecond: the edit must come after the second note was made.
        Thread.Sleep(5);
        _ana.Notes.Edit(first.Id, first.Version, "First", "changed");

        Assert.Equal(["First", "Second"], _ana.Notes.List(new NoteQuery()).Items.Select(note => note.Title));
    }

    [Fact]
    public void TheListFiltersByFolderByNoFolderAndByTag()
    {
        Folder work = _ana.Folders.Create("Work");
        _ana.Notes.Create("In work", folderId: work.Id, tags: ["urgent"]);
        _ana.Notes.Create("In work, calm", folderId: work.Id);
        _ana.Notes.Create("Loose", tags: ["urgent"]);

        Assert.Equal(2, _ana.Notes.List(new NoteQuery(FolderId: work.Id)).Total);
        Assert.Equal(["Loose"], _ana.Notes.List(new NoteQuery(WithoutFolder: true)).Items.Select(note => note.Title));
        Assert.Equal(2, _ana.Notes.List(new NoteQuery(Tag: "URGENT")).Total);
        Assert.Equal(["In work"], _ana.Notes.List(new NoteQuery(FolderId: work.Id, Tag: "urgent")).Items.Select(note => note.Title));
    }

    [Fact]
    public void TheWholeListIsWalkedPageByPageWithoutRepeatingOrSkipping()
    {
        for (int i = 0; i < 7; i++)
        {
            _ana.Notes.Create($"Note {i}");
        }

        List<long> walked = [];
        for (int offset = 0; offset < 7; offset += 3)
        {
            Page<NoteSummary> page = _ana.Notes.List(new NoteQuery(Limit: 3, Offset: offset));
            Assert.Equal(7, page.Total);
            walked.AddRange(page.Items.Select(note => note.Id));
        }

        Assert.Equal(7, walked.Distinct().Count());
    }

    [Theory]
    [InlineData(0, 0, "limit")]
    [InlineData(101, 0, "limit")]
    [InlineData(10, -1, "offset")]
    public void APageOutsideItsBoundsIsRefused(int limit, int offset, string field)
    {
        Assert.Equal(field, Assert.Throws<ValidationException>(() => _ana.Notes.List(new NoteQuery(Limit: limit, Offset: offset))).Field);
    }

    [Fact]
    public void APageOfOneAndOfOneHundredIsAccepted()
    {
        _ana.Notes.List(new NoteQuery(Limit: 1));
        _ana.Notes.List(new NoteQuery(Limit: 100));
    }

    [Fact]
    public void AListOfOneFolderAndOfNoFolderAtOnceIsRefused()
    {
        Assert.Equal("folderId", Assert.Throws<ValidationException>(() => _ana.Notes.List(new NoteQuery(FolderId: 1, WithoutFolder: true))).Field);
    }

    [Fact]
    public void AListTakesNoSearchText()
    {
        Assert.Equal("text", Assert.Throws<ValidationException>(() => _ana.Notes.List(new NoteQuery(Text: "bread"))).Field);
    }

    // -- Pinning and the order of the list -- //

    [Fact]
    public void ANotePinnedComesFirstInTheList()
    {
        Note pinned = _ana.Notes.Create("Older");
        Note newer = _ana.Notes.Create("Newer");

        _ana.Notes.Pin(pinned.Id, pinned: true);

        Assert.Equal([pinned.Id, newer.Id], _ana.Notes.List(new NoteQuery()).Items.Select(note => note.Id));
        Assert.True(_ana.Notes.Get(pinned.Id).IsPinned);
    }

    [Fact]
    public void PinningIsNotEditing()
    {
        Note note = _ana.Notes.Create("Groceries");

        Note pinned = _ana.Notes.Pin(note.Id, pinned: true);
        Note unpinned = _ana.Notes.Pin(note.Id, pinned: false);

        Assert.Equal((note.Version, note.UpdatedAt), (pinned.Version, pinned.UpdatedAt));
        Assert.False(unpinned.IsPinned);
    }

    [Fact]
    public void ANoteInTheTrashIsNotPinned()
    {
        Note note = _ana.Notes.Create("Groceries");
        _ana.Notes.Trash(note.Id);

        Assert.Throws<NotFoundException>(() => _ana.Notes.Pin(note.Id, pinned: true));
    }

    [Fact]
    public void TheListIsOrderedByTitleWhenAsked()
    {
        Note banana = _ana.Notes.Create("banana");
        Note apple = _ana.Notes.Create("Apple");
        Note cherry = _ana.Notes.Create("Cherry");

        Assert.Equal([apple.Id, banana.Id, cherry.Id], _ana.Notes.List(new NoteQuery(Order: "title")).Items.Select(note => note.Id));
    }

    [Fact]
    public void TheListIsOrderedByWhenANoteWasMadeWhenAsked()
    {
        Note first = _ana.Notes.Create("First");
        Note second = _ana.Notes.Create("Second");
        SetDays(first.Id, madeDaysAgo: 3, changedDaysAgo: 1);
        SetDays(second.Id, madeDaysAgo: 2, changedDaysAgo: 2);

        Assert.Equal([second.Id, first.Id], _ana.Notes.List(new NoteQuery(Order: "created")).Items.Select(note => note.Id));
        Assert.Equal([first.Id, second.Id], _ana.Notes.List(new NoteQuery()).Items.Select(note => note.Id));
    }

    [Fact]
    public void AnOrderTheListDoesNotHaveIsRefused()
    {
        Assert.Equal("order", Assert.Throws<ValidationException>(() => _ana.Notes.List(new NoteQuery(Order: "random"))).Field);
    }

    // -- The trash -- //

    [Fact]
    public void ANoteInTheTrashLeavesTheListAndTheSearch()
    {
        Note kept = _ana.Notes.Create("Bread for later");
        Note trashed = _ana.Notes.Create("Bread to throw");

        _ana.Notes.Trash(trashed.Id);

        Assert.Equal([kept.Id], _ana.Notes.List(new NoteQuery()).Items.Select(note => note.Id));
        Assert.Equal([kept.Id], _ana.Notes.Search(new NoteQuery(Text: "bread")).Items.Select(result => result.Note.Id));
    }

    [Fact]
    public void TheTrashListsItsNotesTheLastMovedThereFirst()
    {
        Note first = _ana.Notes.Create("First");
        Note second = _ana.Notes.Create("Second");
        _ana.Notes.Create("Kept");

        _ana.Notes.Trash(second.Id);
        _ana.Notes.Trash(first.Id);
        MovedToTrashDaysAgo(second.Id, 2);
        MovedToTrashDaysAgo(first.Id, 1);

        Page<NoteSummary> trash = _ana.Notes.List(new NoteQuery(InTrash: true));
        Assert.Equal([first.Id, second.Id], trash.Items.Select(note => note.Id));
        Assert.All(trash.Items, note => Assert.NotNull(note.DeletedAt));
    }

    [Fact]
    public void ANotePutBackComesBackAsItWas()
    {
        Folder folder = _ana.Folders.Create("Home");
        Note note = _ana.Notes.Create("Groceries", "Bread", folder.Id, ["food"]);

        _ana.Notes.Trash(note.Id);
        Note restored = _ana.Notes.Restore(note.Id);

        Assert.Equal(note with { Tags = [] }, restored with { Tags = [] });
        Assert.Equal(note.Tags, restored.Tags);
        Assert.Equal([note.Id], _ana.Notes.List(new NoteQuery(FolderId: folder.Id)).Items.Select(item => item.Id));
    }

    [Fact]
    public void ANoteWhoseFolderWasDeletedWhileInTheTrashComesBackInNoFolder()
    {
        Folder folder = _ana.Folders.Create("Home");
        Note note = _ana.Notes.Create("Groceries", folderId: folder.Id);
        _ana.Notes.Trash(note.Id);

        _ana.Folders.Remove(folder.Id);

        Assert.Null(_ana.Notes.Restore(note.Id).FolderId);
    }

    [Fact]
    public void ANoteInTheTrashIsReadButNotWritten()
    {
        Note note = _ana.Notes.Create("Groceries", "Bread");
        _ana.Notes.Trash(note.Id);

        Assert.Equal("Bread", _ana.Notes.Get(note.Id).Content);
        Assert.Throws<NotFoundException>(() => _ana.Notes.Edit(note.Id, note.Version, "Groceries", "Milk"));
        Assert.Throws<NotFoundException>(() => _ana.Notes.Move(note.Id, null));
        Assert.Throws<NotFoundException>(() => _ana.Notes.SetTags(note.Id, ["food"]));
        Assert.Throws<NotFoundException>(() => _ana.Notes.Trash(note.Id));
    }

    [Fact]
    public void ANoteNotInTheTrashCannotBePutBack()
    {
        Note note = _ana.Notes.Create("Groceries");

        Assert.Throws<NotFoundException>(() => _ana.Notes.Restore(note.Id));
    }

    [Fact]
    public void EmptyingTheTrashDeletesItsNotesForGoodWithTheirFiles()
    {
        Note kept = _ana.Notes.Create("Kept");
        Note trashed = _ana.Notes.Create("Trashed");
        _ana.Attachments.Add(trashed.Id, "list.txt", "text/plain", [1, 2, 3]);
        _ana.Notes.Trash(trashed.Id);

        Assert.Equal(1, _ana.Notes.EmptyTrash());

        Assert.Throws<NotFoundException>(() => _ana.Notes.Get(trashed.Id));
        Assert.Equal(kept.Id, _ana.Notes.Get(kept.Id).Id);
        Assert.Empty(Directory.GetFiles(_app.DataFolder.UploadsFolder(_ana.Id)));
    }

    [Fact]
    public void ANoteIsDeletedForGoodOnlyAfterThirtyDaysInTheTrash()
    {
        Note old = _ana.Notes.Create("Old");
        Note recent = _ana.Notes.Create("Recent");
        _ana.Notes.Trash(old.Id);
        _ana.Notes.Trash(recent.Id);
        MovedToTrashDaysAgo(old.Id, 31);
        MovedToTrashDaysAgo(recent.Id, 29);

        Assert.Equal(1, _ana.Notes.RemoveExpired());

        Assert.Throws<NotFoundException>(() => _ana.Notes.Get(old.Id));
        Assert.NotNull(_ana.Notes.Get(recent.Id).DeletedAt);
    }

    [Fact]
    public void TheCountsOfFoldersAndTagsLeaveTheTrashOut()
    {
        Folder folder = _ana.Folders.Create("Home");
        _ana.Notes.Create("Kept", folderId: folder.Id, tags: ["food"]);
        Note trashed = _ana.Notes.Create("Trashed", folderId: folder.Id, tags: ["food", "old"]);

        _ana.Notes.Trash(trashed.Id);

        Assert.Equal(1, Assert.Single(_ana.Folders.List()).NoteCount);
        Assert.Equal([new Tag("food", 1)], _ana.Tags.List());
    }

    /// <summary>Moves back in time when a note was made and when it last changed, as if days had passed.</summary>
    private void SetDays(long noteId, int madeDaysAgo, int changedDaysAgo)
    {
        using SqliteConnection connection = _ana.Session.Database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE notes SET created_at = $createdAt, updated_at = $updatedAt WHERE id = $id";
        command.Parameters.AddWithValue("$id", noteId);
        command.Parameters.AddWithValue("$createdAt", DaysAgo(madeDaysAgo));
        command.Parameters.AddWithValue("$updatedAt", DaysAgo(changedDaysAgo));
        command.ExecuteNonQuery();
    }

    private static string DaysAgo(int days) => DateTimeOffset.UtcNow.AddDays(-days).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    /// <summary>Moves back in time the moment a note went to the trash, as if days had passed.</summary>
    private void MovedToTrashDaysAgo(long noteId, int days)
    {
        using SqliteConnection connection = _ana.Session.Database.Connect();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE notes SET deleted_at = $deletedAt WHERE id = $id";
        command.Parameters.AddWithValue("$id", noteId);
        command.Parameters.AddWithValue("$deletedAt", DateTimeOffset.UtcNow.AddDays(-days).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }
}
