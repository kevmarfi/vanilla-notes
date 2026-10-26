using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>The search over the title and the text of the notes, with the cases of the web version.</summary>
public sealed class SearchTests : IDisposable
{
    private readonly TestApplication _app = new();

    private readonly TestApplication.OpenProfile _ana;

    public SearchTests()
    {
        _ana = _app.CreateProfile();
    }

    public void Dispose() => _app.Dispose();

    private List<string> Found(string text, NoteQuery? filters = null) =>
        _ana.Notes.Search((filters ?? new NoteQuery()) with { Text = text }).Items.Select(result => result.Note.Title).ToList();

    [Fact]
    public void ANoteIsFoundByAWordOfItsTextWithAFragmentAroundIt()
    {
        _ana.Notes.Create("Groceries", "Buy bread and milk on the way home");

        SearchResult result = Assert.Single(_ana.Notes.Search(new NoteQuery(Text: "milk")).Items);

        Assert.Equal("Groceries", result.Note.Title);
        Assert.Contains("milk", result.Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFragmentIsPlainTextCutAroundTheMatchOfALongText()
    {
        string words = string.Join(' ', Enumerable.Repeat("filler", 60));
        _ana.Notes.Create("Long", $"{words} **needle** {words}");

        string snippet = _ana.Notes.Search(new NoteQuery(Text: "needle")).Items.Single().Snippet;

        Assert.Contains("needle", snippet, StringComparison.Ordinal);
        Assert.Contains("…", snippet, StringComparison.Ordinal);
        Assert.True(snippet.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 14);
    }

    [Fact]
    public void ANoteIsFoundByAWordOfItsTitle()
    {
        _ana.Notes.Create("Groceries", "");

        Assert.Equal(["Groceries"], Found("groceries"));
    }

    [Fact]
    public void CapitalsAndAccentsMakeNoDifference()
    {
        _ana.Notes.Create("Canción", "Una canción para el niño en el café");

        Assert.Equal(["Canción"], Found("CANCION nino cafe"));
    }

    [Fact]
    public void EveryWordMustBeFoundInAnyOrder()
    {
        _ana.Notes.Create("Both", "bread and milk");
        _ana.Notes.Create("One", "only bread");

        Assert.Equal(["Both"], Found("milk bread"));
    }

    [Fact]
    public void OnlyWholeWordsMatch()
    {
        _ana.Notes.Create("Breadth", "the breadth of it");

        Assert.Empty(Found("bread"));
    }

    [Fact]
    public void AnEditedNoteIsFoundByItsNewTextAndNoLongerByTheOld()
    {
        Note note = _ana.Notes.Create("Note", "old words");

        _ana.Notes.Edit(note.Id, note.Version, "Note", "new words");

        Assert.Empty(Found("old"));
        Assert.Equal(["Note"], Found("new"));
    }

    [Fact]
    public void ADeletedNoteIsNoLongerFound()
    {
        Note note = _ana.Notes.Create("Note", "words");

        _ana.Notes.Remove(note.Id);

        Assert.Empty(Found("words"));
    }

    [Theory]
    [InlineData("\"unclosed")]
    [InlineData("star*")]
    [InlineData("NEAR(a b)")]
    [InlineData("title:x")]
    [InlineData("a\0b")]
    [InlineData("- ^ ( ) { } :")]
    public void QuotesStarsAndOperatorsNeverFail(string text)
    {
        _ana.Notes.Create("Note", "plain words");

        _ana.Notes.Search(new NoteQuery(Text: text));
    }

    [Fact]
    public void OperatorsAreReadAsPlainWords()
    {
        _ana.Notes.Create("Logic", "cats AND dogs");
        _ana.Notes.Create("Cats", "only cats");

        Assert.Equal(["Logic"], Found("cats AND"));
        Assert.Empty(Found("cats NOT dogs"));
    }

    [Fact]
    public void TheSearchCombinesWithTheFiltersOfFolderAndTag()
    {
        Folder work = _ana.Folders.Create("Work");
        _ana.Notes.Create("Work bread", "bread", folderId: work.Id, tags: ["urgent"]);
        _ana.Notes.Create("Home bread", "bread", tags: ["urgent"]);
        _ana.Notes.Create("Calm bread", "bread", folderId: work.Id);

        Assert.Equal(["Work bread"], Found("bread", new NoteQuery(FolderId: work.Id, Tag: "urgent")));
        Assert.Equal(["Home bread"], Found("bread", new NoteQuery(WithoutFolder: true)));
    }

    [Fact]
    public void TheResultsOfASearchArePaged()
    {
        for (int i = 0; i < 5; i++)
        {
            _ana.Notes.Create($"Note {i}", "bread");
        }

        Page<SearchResult> page = _ana.Notes.Search(new NoteQuery(Text: "bread", Limit: 2, Offset: 4));

        Assert.Equal(5, page.Total);
        Assert.Single(page.Items);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public void AnEmptySearchTextIsRefused(string text)
    {
        Assert.Equal("text", Assert.Throws<ValidationException>(() => _ana.Notes.Search(new NoteQuery(Text: text))).Field);
    }

    [Fact]
    public void ASearchTextOfMoreThanOneHundredCharactersIsRefused()
    {
        Assert.Equal("text", Assert.Throws<ValidationException>(() => _ana.Notes.Search(new NoteQuery(Text: new string('a', 101)))).Field);
    }

    [Fact]
    public void TheSearchWorksTheSameOnAProfileWithAPassword()
    {
        TestApplication.OpenProfile zoe = _app.CreateProfile("Zoe", TestApplication.Password);
        zoe.Notes.Create("Groceries", "Bread and milk");

        Assert.Single(zoe.Notes.Search(new NoteQuery(Text: "milk")).Items);
    }
}
