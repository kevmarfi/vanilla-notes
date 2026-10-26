using System.Text;
using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Storage;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>The files attached to a note: what is accepted, where it goes, and how it leaves.</summary>
public sealed class AttachmentServiceTests : IDisposable
{
    /// <summary>The bytes attached in most tests: text, so that they can be looked for on disk.</summary>
    private static readonly byte[] _text = Encoding.UTF8.GetBytes("Bread, milk, and coffee.");

    private readonly TestApplication _app = new();

    private readonly TestApplication.OpenProfile _ana;

    private readonly long _noteId;

    public AttachmentServiceTests()
    {
        _ana = _app.CreateProfile();
        _noteId = _ana.Notes.Create("Groceries").Id;
    }

    public void Dispose() => _app.Dispose();

    [Fact]
    public void NoFileIsAddedToANoteInTheTrash()
    {
        _ana.Notes.Trash(_noteId);

        Assert.Throws<NotFoundException>(() => _ana.Attachments.Add(_noteId, "list.txt", "text/plain", _text));
    }

    [Fact]
    public void AFileIsAttachedListedAndReadBack()
    {
        Attachment attachment = _ana.Attachments.Add(_noteId, "list.txt", "Text/Plain", _text);

        Assert.Equal(new Attachment(attachment.Id, _noteId, "list.txt", "text/plain", _text.Length, attachment.CreatedAt), attachment);
        Assert.Equal([attachment], _ana.Attachments.List(_noteId));
        Assert.Equal(_text, _ana.Attachments.ReadContent(attachment.Id));
    }

    [Fact]
    public void AFileIsStoredUnderARandomNameNeverTheOneGiven()
    {
        _ana.Attachments.Add(_noteId, "list.txt", "text/plain", _text);

        string stored = Assert.Single(Directory.GetFiles(_app.DataFolder.UploadsFolder(_ana.Id)));

        Assert.Matches("^[0-9a-f]{32}$", Path.GetFileName(stored));
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    public void EveryAllowedKindIsAccepted(string mimeType)
    {
        _ana.Attachments.Add(_noteId, "file", mimeType, _text);
    }

    [Fact]
    public void AKindNotAllowedIsRefusedAndNothingIsStored()
    {
        Assert.Equal("mimeType", Assert.Throws<ValidationException>(() => _ana.Attachments.Add(_noteId, "page.html", "text/html", _text)).Field);
        Assert.Empty(Directory.GetFiles(_app.DataFolder.UploadsFolder(_ana.Id)));
    }

    [Fact]
    public void AFileOfFiveMegabytesIsAcceptedAndOneByteMoreIsNot()
    {
        _ana.Attachments.Add(_noteId, "big.pdf", "application/pdf", new byte[5 * 1024 * 1024]);

        Assert.Equal("file", Assert.Throws<ValidationException>(() => _ana.Attachments.Add(_noteId, "bigger.pdf", "application/pdf", new byte[(5 * 1024 * 1024) + 1])).Field);
    }

    [Fact]
    public void AnEmptyFileIsRefused()
    {
        Assert.Equal("file", Assert.Throws<ValidationException>(() => _ana.Attachments.Add(_noteId, "empty.txt", "text/plain", [])).Field);
    }

    [Theory]
    [InlineData("")]
    [InlineData("folder/list.txt")]
    [InlineData("..\\list.txt")]
    public void ANameThatIsEmptyOrHasASlashIsRefused(string fileName)
    {
        Assert.Equal("fileName", Assert.Throws<ValidationException>(() => _ana.Attachments.Add(_noteId, fileName, "text/plain", _text)).Field);
    }

    [Fact]
    public void ANoteThatDoesNotExistTakesNoFile()
    {
        Assert.Throws<NotFoundException>(() => _ana.Attachments.Add(999, "list.txt", "text/plain", _text));
        Assert.Throws<NotFoundException>(() => _ana.Attachments.List(999));
    }

    [Fact]
    public void RemovingAFileTakesItFromTheDisk()
    {
        Attachment attachment = _ana.Attachments.Add(_noteId, "list.txt", "text/plain", _text);

        _ana.Attachments.Remove(attachment.Id);

        Assert.Empty(_ana.Attachments.List(_noteId));
        Assert.Empty(Directory.GetFiles(_app.DataFolder.UploadsFolder(_ana.Id)));
        Assert.Throws<NotFoundException>(() => _ana.Attachments.Remove(attachment.Id));
        Assert.Throws<NotFoundException>(() => _ana.Attachments.ReadContent(attachment.Id));
    }

    [Fact]
    public void DeletingANoteDeletesItsFiles()
    {
        _ana.Attachments.Add(_noteId, "list.txt", "text/plain", _text);

        _ana.Notes.Remove(_noteId);

        Assert.Empty(Directory.GetFiles(_app.DataFolder.UploadsFolder(_ana.Id)));
    }

    [Fact]
    public void TheFilesOfAProfileWithAPasswordAreSealedOnDisk()
    {
        TestApplication.OpenProfile zoe = _app.CreateProfile("Zoe", TestApplication.Password);
        long noteId = zoe.Notes.Create("Groceries").Id;

        Attachment attachment = zoe.Attachments.Add(noteId, "list.txt", "text/plain", _text);

        Assert.False(TestApplication.DiskHolds(_app.DataFolder.UploadsFolder(zoe.Id), "Bread, milk"));
        Assert.Equal(_text, zoe.Attachments.ReadContent(attachment.Id));
    }

    [Fact]
    public void SettingAPasswordSealsTheFilesThereAre()
    {
        Attachment attachment = _ana.Attachments.Add(_noteId, "list.txt", "text/plain", _text);

        _app.ProfileService.SetPassword(_ana.Session, TestApplication.Password);

        Assert.False(TestApplication.DiskHolds(_app.DataFolder.UploadsFolder(_ana.Id), "Bread, milk"));
        Assert.Equal(_text, _ana.Attachments.ReadContent(attachment.Id));
    }

    [Fact]
    public void AnAttachedImageIsReadForTheNoteToShow()
    {
        Attachment image = _ana.Attachments.Add(_noteId, "cat.png", "image/png", _text);

        Assert.Equal(_text, _ana.Attachments.ReadImage(image.Id));
    }

    [Fact]
    public void AFileThatIsNotAnImageOrIsMissingIsNotShownAsOne()
    {
        Attachment text = _ana.Attachments.Add(_noteId, "list.txt", "text/plain", _text);

        Assert.Null(_ana.Attachments.ReadImage(text.Id));
        Assert.Null(_ana.Attachments.ReadImage(text.Id + 100));
    }

    [Fact]
    public void FilesNoRecordPointsAtAreRemoved()
    {
        Attachment kept = _ana.Attachments.Add(_noteId, "list.txt", "text/plain", _text);
        _ana.Session.Files.Save(_text);

        Assert.Equal(1, _ana.Attachments.RemoveOrphanFiles());
        Assert.Equal(_text, _ana.Attachments.ReadContent(kept.Id));
    }

    [Fact]
    public void NothingIsAttachedWhileTheDiskIsRunningOut()
    {
        var full = new AttachmentService(
            new AttachmentRepository(_ana.Session.Database),
            new NoteRepository(_ana.Session.Database),
            _ana.Session,
            new DiskSpace(_app.DataFolder.Root, long.MaxValue));

        Assert.Equal(ErrorCodes.StorageFull, Assert.Throws<StorageException>(() => full.Add(_noteId, "list.txt", "text/plain", _text)).Code);
        Assert.Empty(Directory.GetFiles(_app.DataFolder.UploadsFolder(_ana.Id)));
    }
}
