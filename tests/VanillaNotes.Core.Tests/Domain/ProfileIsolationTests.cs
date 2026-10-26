using VanillaNotes.Core.Models;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>What one profile has is not seen from another.</summary>
public sealed class ProfileIsolationTests : IDisposable
{
    private readonly TestApplication _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public void TheNotesOfOneProfileAreNotListedInAnother()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile("Ana");
        TestApplication.OpenProfile zoe = _app.CreateProfile("Zoe");

        ana.Notes.Create("A note of Ana", "groceries", tags: ["home"]);
        ana.Folders.Create("Work");

        Assert.Equal(0, zoe.Notes.List(new NoteQuery()).Total);
        Assert.Equal(0, zoe.Notes.Search(new NoteQuery(Text: "groceries")).Total);
        Assert.Empty(zoe.Tags.List());
        Assert.Empty(zoe.Folders.List());
    }

    [Fact]
    public void EachProfileHasItsOwnFolderOnDisk()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile("Ana");
        TestApplication.OpenProfile zoe = _app.CreateProfile("Zoe");

        Assert.NotEqual(_app.DataFolder.ProfileDatabasePath(ana.Id), _app.DataFolder.ProfileDatabasePath(zoe.Id));
        Assert.True(File.Exists(_app.DataFolder.ProfileDatabasePath(ana.Id)));
        Assert.True(File.Exists(_app.DataFolder.ProfileDatabasePath(zoe.Id)));
    }

    [Fact]
    public void ThePasswordOfOneProfileDoesNotOpenAnother()
    {
        long ana = _app.CreateProfile("Ana", "the password of ana").Id;
        long zoe = _app.CreateProfile("Zoe", "the password of zoe").Id;

        Assert.Throws<VanillaNotes.Core.Errors.WrongPasswordException>(() => _app.ProfileService.Open(zoe, "the password of ana"));
        Assert.Throws<VanillaNotes.Core.Errors.WrongPasswordException>(() => _app.ProfileService.Open(ana, "the password of zoe"));
    }
}
