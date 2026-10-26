using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>The folders of a profile: one level, unique names, and notes left without a folder when it goes.</summary>
public sealed class FolderServiceTests : IDisposable
{
    private readonly TestApplication _app = new();

    private readonly TestApplication.OpenProfile _ana;

    public FolderServiceTests()
    {
        _ana = _app.CreateProfile();
    }

    public void Dispose() => _app.Dispose();

    [Fact]
    public void FoldersAreListedByNameWithHowManyNotesTheyHold()
    {
        Folder work = _ana.Folders.Create("Work");
        _ana.Folders.Create(" Home ");
        _ana.Notes.Create("A note", folderId: work.Id);

        IReadOnlyList<Folder> folders = _ana.Folders.List();

        Assert.Equal(["Home", "Work"], folders.Select(folder => folder.Name));
        Assert.Equal([0, 1], folders.Select(folder => folder.NoteCount));
    }

    [Fact]
    public void TwoFoldersCannotHaveTheSameNameRegardlessOfCase()
    {
        _ana.Folders.Create("Work");

        Assert.Equal(ErrorCodes.FolderExists, Assert.Throws<ConflictException>(() => _ana.Folders.Create("WORK")).Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AFolderNeedsAName(string name)
    {
        Assert.Equal("name", Assert.Throws<ValidationException>(() => _ana.Folders.Create(name)).Field);
    }

    [Fact]
    public void ANameOfMoreThanOneHundredCharactersIsRefused()
    {
        Assert.Equal("name", Assert.Throws<ValidationException>(() => _ana.Folders.Create(new string('a', 101))).Field);
    }

    [Fact]
    public void AFolderIsRenamed()
    {
        Folder work = _ana.Folders.Create("Work");
        _ana.Folders.Create("Home");

        Assert.Equal("Office", _ana.Folders.Rename(work.Id, "Office").Name);
        Assert.Equal(ErrorCodes.FolderExists, Assert.Throws<ConflictException>(() => _ana.Folders.Rename(work.Id, "home")).Code);
        Assert.Throws<NotFoundException>(() => _ana.Folders.Rename(999, "Anything"));
    }

    [Fact]
    public void DeletingAFolderLeavesItsNotesInNone()
    {
        Folder work = _ana.Folders.Create("Work");
        Note note = _ana.Notes.Create("A note", folderId: work.Id);

        _ana.Folders.Remove(work.Id);

        Assert.Null(_ana.Notes.Get(note.Id).FolderId);
        Assert.Throws<NotFoundException>(() => _ana.Folders.Remove(work.Id));
    }
}
