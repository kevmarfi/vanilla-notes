using VanillaNotes.Core.Models;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>The tags of a profile exist while a note carries them.</summary>
public sealed class TagServiceTests : IDisposable
{
    private readonly TestApplication _app = new();

    private readonly TestApplication.OpenProfile _ana;

    public TagServiceTests()
    {
        _ana = _app.CreateProfile();
    }

    public void Dispose() => _app.Dispose();

    [Fact]
    public void TagsAreListedInOrderWithHowManyNotesCarryThem()
    {
        _ana.Notes.Create("One", tags: ["work", "Ideas"]);
        _ana.Notes.Create("Two", tags: ["work"]);

        Assert.Equal([new Tag("ideas", 1), new Tag("work", 2)], _ana.Tags.List());
    }

    [Fact]
    public void ATagNoNoteCarriesIsGone()
    {
        Note note = _ana.Notes.Create("One", tags: ["work", "ideas"]);
        _ana.Notes.Create("Two", tags: ["work"]);

        _ana.Notes.SetTags(note.Id, ["work"]);

        Assert.Equal(["work"], _ana.Tags.List().Select(tag => tag.Name));
    }

    [Fact]
    public void DeletingTheLastNoteOfATagTakesTheTag()
    {
        Note note = _ana.Notes.Create("One", tags: ["ideas"]);

        _ana.Notes.Remove(note.Id);

        Assert.Empty(_ana.Tags.List());
    }
}
