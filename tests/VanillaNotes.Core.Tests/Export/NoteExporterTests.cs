using System.IO.Compression;
using System.Text;
using System.Text.Json;
using VanillaNotes.Core.Export;
using VanillaNotes.Core.Models;
using Xunit;

namespace VanillaNotes.Core.Tests.Export;

/// <summary>What each form of the export holds, with the cases of the web version.</summary>
public sealed class NoteExporterTests
{
    /// <summary>A moment every note of these tests is dated with.</summary>
    private static readonly DateTimeOffset _moment = new(2026, 10, 14, 9, 30, 0, TimeSpan.Zero);

    /// <summary>The folders of these tests, by number.</summary>
    private static readonly Dictionary<long, string> _folders = new() { [1] = "Work" };

    /// <summary>No attached file.</summary>
    private static readonly Dictionary<long, (string MimeType, byte[] Bytes)> _noFiles = [];

    private static Note Note(string title, string content = "", long? folderId = null, string[]? tags = null) =>
        new(1, title, content, folderId, tags ?? [], 1, _moment, _moment);

    private static Dictionary<string, string> Read(byte[] archive)
    {
        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        });
    }

    private static Dictionary<string, string> Export(IReadOnlyList<Note> notes, ExportFormat format, Dictionary<long, (string MimeType, byte[] Bytes)>? files = null) =>
        Read(NoteExporter.Export(notes, _folders, files ?? _noFiles, format, _moment));

    // -- Markdown -- //

    [Fact]
    public void ANoteIsAMarkdownFileWithAHeaderOfWhatMarkdownHasNoPlaceFor()
    {
        Dictionary<string, string> files = Export([Note("Groceries \"weekly\" año", "Bread", tags: ["home", "list"])], ExportFormat.Markdown);

        Assert.Equal(
            "---\ntitle: \"Groceries \\\"weekly\\\" año\"\ntags: [home, list]\ncreated: 2026-10-14T09:30:00.000Z\nupdated: 2026-10-14T09:30:00.000Z\n---\n\nBread\n",
            files["Groceries -weekly- año.md"]);
    }

    [Fact]
    public void ANoteIsInAFolderNamedAfterItsOwnAndOneWithoutAtTheTop()
    {
        Dictionary<string, string> files = Export([Note("Plan", folderId: 1), Note("Loose")], ExportFormat.Markdown);

        Assert.Equal(["Loose.md", "Work/Plan.md"], files.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("a/b\\c:d*e?f<g>h|i", "a-b-c-d-e-f-g-h-i.md")]
    [InlineData(" .hidden. ", "hidden.md")]
    [InlineData("...", "untitled.md")]
    public void WhatAFileNameCannotCarryIsReplaced(string title, string fileName)
    {
        Assert.Equal([fileName], Export([Note(title)], ExportFormat.Markdown).Keys);
    }

    [Fact]
    public void NotesThatWouldGetTheSameFileAreNumberedRegardlessOfCase()
    {
        Dictionary<string, string> files = Export([Note("Plan"), Note("PLAN"), Note("plan")], ExportFormat.Markdown);

        Assert.Equal(["PLAN (2).md", "Plan.md", "plan (3).md"], files.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheAttachedFilesTheNotesPointAtAreAddedAndPointedAt()
    {
        var attached = new Dictionary<long, (string MimeType, byte[] Bytes)> { [7] = ("image/png", [1, 2]), [8] = ("text/plain", Encoding.UTF8.GetBytes("text")) };

        Dictionary<string, string> files = Export([Note("Loose", "![cat](attachment:7)"), Note("Plan", "[list](attachment:8)", folderId: 1)], ExportFormat.Markdown, attached);

        Assert.Contains("attachments/7.png", files.Keys);
        Assert.Equal("text", files["attachments/8.txt"]);
        Assert.EndsWith("![cat](attachments/7.png)\n", files["Loose.md"], StringComparison.Ordinal);
        Assert.EndsWith("[list](../attachments/8.txt)\n", files["Work/Plan.md"], StringComparison.Ordinal);
    }

    [Fact]
    public void AnAddressOfAFileThatIsNotThereIsLeftAsItIs()
    {
        Assert.EndsWith("![cat](attachment:9)\n", Export([Note("Loose", "![cat](attachment:9)")], ExportFormat.Markdown)["Loose.md"], StringComparison.Ordinal);
    }

    // -- Web pages -- //

    [Fact]
    public void ANoteIsAWebPageWithItsTitleAndTheNoteFormatted()
    {
        string page = Export([Note("Groceries", "**Bread**", tags: ["home"])], ExportFormat.Html)["Groceries.html"];

        Assert.Contains("<title>Groceries</title>", page, StringComparison.Ordinal);
        Assert.Contains("<h1>Groceries</h1>", page, StringComparison.Ordinal);
        Assert.Contains("<strong>Bread</strong>", page, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"keywords\" content=\"home\">", page, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"date\" content=\"2026-10-14T09:30:00.000Z\">", page, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTitleIsWrittenSafelyWhateverItHolds()
    {
        string page = Export([Note("<script>alert(1)</script>")], ExportFormat.Html).Values.Single();

        Assert.DoesNotContain("<script>", page, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", page, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAttachedImageIsCarriedInsideThePageAndNoFileIsAdded()
    {
        var attached = new Dictionary<long, (string MimeType, byte[] Bytes)> { [7] = ("image/png", [1, 2, 3]) };

        Dictionary<string, string> files = Export([Note("Cat", "![cat](attachment:7)")], ExportFormat.Html, attached);

        Assert.Equal(["Cat.html"], files.Keys);
        Assert.Contains("src=\"data:image/png;base64,AQID\"", files["Cat.html"], StringComparison.Ordinal);
    }

    // -- Data -- //

    [Fact]
    public void TheDataIsOneFileThatReadsBackToWhatWasExported()
    {
        byte[] bytes = NoteExporter.Export([Note("Plan", "Text", folderId: 1, tags: ["work"]), Note("Loose")], _folders, _noFiles, ExportFormat.Data, _moment);

        using JsonDocument data = JsonDocument.Parse(bytes);
        JsonElement plan = data.RootElement.GetProperty("notes")[0];
        Assert.Equal("2026-10-14T09:30:00.000Z", data.RootElement.GetProperty("exportedAt").GetString());
        Assert.Equal("Plan", plan.GetProperty("title").GetString());
        Assert.Equal("Text", plan.GetProperty("content").GetString());
        Assert.Equal("Work", plan.GetProperty("folder").GetString());
        Assert.Equal("work", plan.GetProperty("tags")[0].GetString());
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("notes")[1].GetProperty("folder").ValueKind);
        Assert.Equal(".json", NoteExporter.Extension(ExportFormat.Data));
    }

    [Fact]
    public void AnArchiveOfNoNotesIsStillAnArchive()
    {
        Assert.Empty(Export([], ExportFormat.Markdown));
        Assert.Equal(".zip", NoteExporter.Extension(ExportFormat.Markdown));
    }
}
