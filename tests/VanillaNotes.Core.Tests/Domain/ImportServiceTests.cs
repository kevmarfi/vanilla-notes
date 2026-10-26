using System.Globalization;
using System.Text;
using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Export;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Storage;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>The data file the web version exports, and a folder of Markdown files, brought into a profile.</summary>
public sealed class ImportServiceTests : IDisposable
{
    /// <summary>An export of the web version, as GET /account/export answers it and its settings save it.</summary>
    private const string _webExport = """
        {
          "exportedAt": "2026-10-04T09:15:00.000Z",
          "notes": [
            {
              "title": "HTTP verbs",
              "content": "# Verbs\n\n- GET\n- POST\n\n![diagram](/api/attachments/12)",
              "folder": "Networking",
              "tags": ["http", "networking"],
              "createdAt": "2026-10-03T18:30:00.000Z",
              "updatedAt": "2026-10-03T19:45:00.000Z"
            },
            {
              "title": "Groceries",
              "content": "Bread",
              "folder": null,
              "tags": [],
              "createdAt": "2026-09-01T08:00:00.000Z",
              "updatedAt": "2026-09-02T08:00:00.000Z"
            }
          ]
        }
        """;

    private readonly TestApplication _app = new();

    private readonly TestApplication.OpenProfile _ana;

    public ImportServiceTests()
    {
        _ana = _app.CreateProfile();
    }

    public void Dispose() => _app.Dispose();

    private ImportService ImportOn(TestApplication.OpenProfile profile, bool diskIsFull = false) => new(
        new NoteRepository(profile.Session.Database),
        new FolderRepository(profile.Session.Database),
        profile.Attachments,
        diskIsFull ? new DiskSpace(_app.DataFolder.Root, long.MaxValue) : _app.DiskSpace);

    private static byte[] File(string json) => Encoding.UTF8.GetBytes(json);

    [Fact]
    public void TheNotesOfAnExportOfTheWebVersionAreBroughtInWithTheirFolderTagsAndDates()
    {
        Assert.Equal(2, ImportOn(_ana).Import(File(_webExport)));

        Folder networking = Assert.Single(_ana.Folders.List());
        Assert.Equal("Networking", networking.Name);
        NoteSummary verbs = Assert.Single(_ana.Notes.List(new NoteQuery(FolderId: networking.Id)).Items);
        Note note = _ana.Notes.Get(verbs.Id);
        Assert.Equal("HTTP verbs", note.Title);
        Assert.Equal(["http", "networking"], note.Tags);
        Assert.Equal(DateTimeOffset.Parse("2026-10-03T18:30:00Z", CultureInfo.InvariantCulture), note.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-10-03T19:45:00Z", CultureInfo.InvariantCulture), note.UpdatedAt);
        Assert.Equal(1, _ana.Notes.List(new NoteQuery(WithoutFolder: true)).Total);
    }

    [Fact]
    public void TheAddressOfAnAttachmentOfTheServerIsLeftAsItWas()
    {
        ImportOn(_ana).Import(File(_webExport));

        NoteSummary verbs = _ana.Notes.Search(new NoteQuery(Text: "verbs")).Items[0].Note;
        Assert.EndsWith("![diagram](/api/attachments/12)", _ana.Notes.Get(verbs.Id).Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ANoteGoesIntoTheFolderOfItsNameRegardlessOfCase()
    {
        Folder existing = _ana.Folders.Create("networking");

        ImportOn(_ana).Import(File(_webExport));

        Assert.Equal(existing.Id, Assert.Single(_ana.Folders.List()).Id);
        Assert.Equal(1, _ana.Notes.List(new NoteQuery(FolderId: existing.Id)).Total);
    }

    [Fact]
    public void TheNotesTheProfileHasAreNotTouched()
    {
        Note mine = _ana.Notes.Create("Mine", "Kept");

        ImportOn(_ana).Import(File(_webExport));

        Assert.Equal(3, _ana.Notes.List(new NoteQuery()).Total);
        Note kept = _ana.Notes.Get(mine.Id);
        Assert.Equal((mine.Title, mine.Content, mine.Version), (kept.Title, kept.Content, kept.Version));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("""{ "notes": 3 }""")]
    [InlineData("""{ "notes": [ { "title": "No dates", "content": "", "folder": null, "tags": [] } ] }""")]
    [InlineData("""{ "notes": [ { "title": 7, "content": "", "folder": null, "tags": [], "createdAt": "2026-10-03T18:30:00.000Z", "updatedAt": "2026-10-03T18:30:00.000Z" } ] }""")]
    [InlineData("""{ "notes": [ { "title": "Bad date", "content": "", "folder": null, "tags": [], "createdAt": "yesterday", "updatedAt": "2026-10-03T18:30:00.000Z" } ] }""")]
    public void AFileThatIsNotAnExportIsRefused(string json)
    {
        Assert.Equal("exportFile", Assert.Throws<ValidationException>(() => ImportOn(_ana).Import(File(json))).Field);
        Assert.Equal(0, _ana.Notes.List(new NoteQuery()).Total);
    }

    [Fact]
    public void ANoteThatBreaksARuleBringsInNoneOfTheFile()
    {
        string json = _webExport.Replace("\"Groceries\"", "\"\"", StringComparison.Ordinal);

        Assert.Equal("title", Assert.Throws<ValidationException>(() => ImportOn(_ana).Import(File(json))).Field);
        Assert.Equal(0, _ana.Notes.List(new NoteQuery()).Total);
        Assert.Empty(_ana.Folders.List());
    }

    [Fact]
    public void NothingIsBroughtInWhileTheDiskIsRunningOut()
    {
        Assert.Equal(ErrorCodes.StorageFull, Assert.Throws<StorageException>(() => ImportOn(_ana, diskIsFull: true).Import(File(_webExport))).Code);
        Assert.Equal(0, _ana.Notes.List(new NoteQuery()).Total);
    }

    [Fact]
    public void TheDataFileThisApplicationExportsIsBroughtInToo()
    {
        _ana.Notes.Create("Exported", "From the desktop", tags: ["mine"]);
        byte[] exported = new ExportService(
            new NoteRepository(_ana.Session.Database),
            new FolderRepository(_ana.Session.Database),
            new AttachmentRepository(_ana.Session.Database),
            _ana.Session).Export(ExportFormat.Data);
        TestApplication.OpenProfile bea = _app.CreateProfile("Bea");

        Assert.Equal(1, ImportOn(bea).Import(exported));
        Assert.Equal("Exported", Assert.Single(bea.Notes.List(new NoteQuery()).Items).Title);
    }

    // -- A folder of Markdown files -- //

    [Fact]
    public void EachMarkdownFileIsANoteTitledByItsNameInTheFolderOfItsFirstLevel()
    {
        using var source = new SourceFolder();
        source.Write("Groceries.md", "Bread");
        source.Write("Work/Plan.md", "Ship it");
        source.Write("Work/Old/Notes.markdown", "Deeper");

        FolderImport outcome = ImportOn(_ana).ImportFolder(source.Root);

        Assert.Equal(new FolderImport(3, 0, 0), outcome);
        Folder work = Assert.Single(_ana.Folders.List());
        Assert.Equal("Work", work.Name);
        Assert.Equal(["Notes", "Plan"], _ana.Notes.List(new NoteQuery(FolderId: work.Id, Order: "title")).Items.Select(note => note.Title));
        Assert.Equal("Bread", _ana.Notes.Get(_ana.Notes.List(new NoteQuery(WithoutFolder: true)).Items.Single().Id).Content);
    }

    [Fact]
    public void AnImageANotePointsAtIsAttachedAndItsAddressRewritten()
    {
        using var source = new SourceFolder();
        source.Write("Trip.md", "Look: ![the view](images/view%20one.png) and again ![](images/view%20one.png)");
        source.WriteBytes("images/view one.png", [1, 2, 3]);

        FolderImport outcome = ImportOn(_ana).ImportFolder(source.Root);

        Assert.Equal(new FolderImport(1, 1, 0), outcome);
        Note note = _ana.Notes.Get(_ana.Notes.List(new NoteQuery()).Items.Single().Id);
        Attachment image = Assert.Single(_ana.Attachments.List(note.Id));
        Assert.Equal(("view one.png", "image/png"), (image.FileName, image.MimeType));
        Assert.Equal($"Look: ![the view](attachment:{image.Id}) and again ![](attachment:{image.Id})", note.Content);
        Assert.Equal(1, note.Version);
    }

    [Fact]
    public void AnAddressOutsideTheFolderOfTheWebOrInCodeIsLeftAsItIs()
    {
        using var source = new SourceFolder();
        string text = "![out](../outside.png) ![web](https://example.com/a.png) ![gone](missing.png)\n\n    ![code](inside.png)";
        source.Write("Note.md", text);
        source.WriteBytes("inside.png", [1]);
        System.IO.File.WriteAllBytes(Path.Combine(source.Root, "..", "outside.png"), [1]);

        FolderImport outcome = ImportOn(_ana).ImportFolder(source.Root);

        Assert.Equal(new FolderImport(1, 0, 1), outcome);
        Assert.Equal(text, _ana.Notes.Get(_ana.Notes.List(new NoteQuery()).Items.Single().Id).Content);
    }

    [Fact]
    public void WhatIsNeitherANoteNorAnImageOfOneIsLeftOutAndCounted()
    {
        using var source = new SourceFolder();
        source.Write("Note.md", "Text");
        source.Write("readme.txt", "Not a note");
        source.Write(new string('a', 10) + ".md", new string('x', 100_001));
        source.Write(".obsidian/workspace.md", "Settings of another application");

        FolderImport outcome = ImportOn(_ana).ImportFolder(source.Root);

        Assert.Equal(new FolderImport(1, 0, 2), outcome);
        Assert.Equal("Note", Assert.Single(_ana.Notes.List(new NoteQuery()).Items).Title);
    }

    [Fact]
    public void ANoteKeepsTheDatesOfItsFile()
    {
        using var source = new SourceFolder();
        source.Write("Old.md", "Text");
        var changed = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        System.IO.File.SetCreationTimeUtc(Path.Combine(source.Root, "Old.md"), changed.AddDays(-1));
        System.IO.File.SetLastWriteTimeUtc(Path.Combine(source.Root, "Old.md"), changed);

        ImportOn(_ana).ImportFolder(source.Root);

        NoteSummary note = Assert.Single(_ana.Notes.List(new NoteQuery()).Items);
        Assert.Equal((changed.AddDays(-1), changed), (note.CreatedAt.UtcDateTime, note.UpdatedAt.UtcDateTime));
    }

    [Fact]
    public void NothingOfAFolderIsBroughtInWhileTheDiskIsRunningOut()
    {
        using var source = new SourceFolder();
        source.Write("Note.md", "Text");

        Assert.Equal(ErrorCodes.StorageFull, Assert.Throws<StorageException>(() => ImportOn(_ana, diskIsFull: true).ImportFolder(source.Root)).Code);
        Assert.Equal(0, _ana.Notes.List(new NoteQuery()).Total);
    }

    /// <summary>A folder of Markdown files made for a test, inside a folder of its own so that a note can point outside it, and removed after it.</summary>
    private sealed class SourceFolder : IDisposable
    {
        private readonly DirectoryInfo _outer = Directory.CreateTempSubdirectory("vanilla-notes-tests-");

        public SourceFolder()
        {
            Root = Directory.CreateDirectory(Path.Combine(_outer.FullName, "vault")).FullName;
        }

        /// <summary>The folder picked.</summary>
        public string Root { get; }

        public void Write(string relativePath, string text) => WriteBytes(relativePath, Encoding.UTF8.GetBytes(text));

        public void WriteBytes(string relativePath, byte[] bytes)
        {
            string path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllBytes(path, bytes);
        }

        public void Dispose() => _outer.Delete(recursive: true);
    }
}
