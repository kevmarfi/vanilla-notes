using VanillaNotes.Core.Models;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>Where the person was in the workspace of a profile, kept for the next time it is opened.</summary>
public sealed class WorkspaceServiceTests : IDisposable
{
    private readonly TestApplication _app = new();

    private readonly TestApplication.OpenProfile _ana;

    public WorkspaceServiceTests()
    {
        _ana = _app.CreateProfile();
    }

    public void Dispose() => _app.Dispose();

    [Fact]
    public void AProfileNeverLeftIsOnTheWholeListWithNoNoteOpen()
    {
        Assert.Equal(new WorkspacePlace(null, null, false), _ana.Workspace.Get());
    }

    [Fact]
    public void ThePlaceIsKeptByTheProfile()
    {
        Folder folder = _ana.Folders.Create("Work");
        Note note = _ana.Notes.Create("Plan", folderId: folder.Id);

        _ana.Workspace.Remember(new WorkspacePlace(note.Id, folder.Id, false));
        _ana.Session.Dispose();

        Assert.Equal(new WorkspacePlace(note.Id, folder.Id, false), _app.Open(_ana.Id).Workspace.Get());
    }

    [Fact]
    public void TheNotesInNoFolderAreKeptAsTheChoice()
    {
        _ana.Workspace.Remember(new WorkspacePlace(null, null, true));

        Assert.Equal(new WorkspacePlace(null, null, true), _ana.Workspace.Get());
    }

    [Fact]
    public void AFolderChosenIsNotAlsoTheNotesInNoFolder()
    {
        Folder folder = _ana.Folders.Create("Work");

        _ana.Workspace.Remember(new WorkspacePlace(null, folder.Id, true));

        Assert.Equal(new WorkspacePlace(null, folder.Id, false), _ana.Workspace.Get());
    }

    [Fact]
    public void ANoteDeletedSinceIsForgotten()
    {
        Note note = _ana.Notes.Create("Plan");
        _ana.Workspace.Remember(new WorkspacePlace(note.Id, null, false));

        _ana.Notes.Remove(note.Id);

        Assert.Null(_ana.Workspace.Get().NoteId);
    }

    [Fact]
    public void AFolderDeletedSinceIsForgotten()
    {
        Folder folder = _ana.Folders.Create("Work");
        _ana.Workspace.Remember(new WorkspacePlace(null, folder.Id, false));

        _ana.Folders.Remove(folder.Id);

        Assert.Null(_ana.Workspace.Get().FolderId);
    }

    [Fact]
    public void ANoteMovedToTheTrashSinceIsNotOpenedAgain()
    {
        Note note = _ana.Notes.Create("Plan");
        _ana.Workspace.Remember(new WorkspacePlace(note.Id, null, false));

        _ana.Notes.Trash(note.Id);

        Assert.Null(_ana.Workspace.Get().NoteId);
    }

    [Fact]
    public void ANoteOrAFolderThatIsNotThereIsKeptAsNone()
    {
        _ana.Workspace.Remember(new WorkspacePlace(999, 999, false));

        Assert.Equal(new WorkspacePlace(null, null, false), _ana.Workspace.Get());
    }
}
