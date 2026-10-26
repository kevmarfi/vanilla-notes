using System.IO.Compression;
using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Export;
using VanillaNotes.Core.Markdown;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>Every note of a profile taken out, with the files the notes point at.</summary>
public sealed class ExportServiceTests : IDisposable
{
    private readonly TestApplication _app = new();

    public void Dispose() => _app.Dispose();

    private static ExportService ExportOf(TestApplication.OpenProfile open) =>
        new(new NoteRepository(open.Session.Database), new FolderRepository(open.Session.Database), new AttachmentRepository(open.Session.Database), open.Session);

    [Fact]
    public void TheFilesOfAProfileWithAPasswordLeavePlain()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile(password: TestApplication.Password);
        long noteId = ana.Notes.Create("Cat").Id;
        long fileId = ana.Attachments.Add(noteId, "cat.png", "image/png", [1, 2, 3]).Id;
        ana.Notes.Edit(noteId, 1, "Cat", $"![cat]({MarkdownParser.AttachmentAddress(fileId)})");

        using var zip = new ZipArchive(new MemoryStream(ExportOf(ana).Export(ExportFormat.Markdown)), ZipArchiveMode.Read);

        using var image = new MemoryStream();
        zip.GetEntry($"attachments/{fileId}.png")!.Open().CopyTo(image);
        Assert.Equal([1, 2, 3], image.ToArray());
        Assert.NotNull(zip.GetEntry("Cat.md"));
    }

    [Fact]
    public void ANoteInTheTrashIsNotTakenOut()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile();
        ana.Notes.Create("Kept");
        ana.Notes.Trash(ana.Notes.Create("Trashed").Id);

        using var zip = new ZipArchive(new MemoryStream(ExportOf(ana).Export(ExportFormat.Markdown)), ZipArchiveMode.Read);

        Assert.Equal(["Kept.md"], zip.Entries.Select(entry => entry.FullName));
    }

    [Fact]
    public void AFileOfAnotherNoteOrThatIsGoneIsNotTakenOut()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile();
        ana.Notes.Create("Note", "![gone](attachment:42)");

        using var zip = new ZipArchive(new MemoryStream(ExportOf(ana).Export(ExportFormat.Markdown)), ZipArchiveMode.Read);

        Assert.Equal(["Note.md"], zip.Entries.Select(entry => entry.FullName));
    }
}
