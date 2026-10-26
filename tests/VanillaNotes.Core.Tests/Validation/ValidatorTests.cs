using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Validation;
using Xunit;

namespace VanillaNotes.Core.Tests.Validation;

/// <summary>What the validators accept and give back, apart from the services that call them.</summary>
public sealed class ValidatorTests
{
    [Fact]
    public void ATagMayHoldLettersOfAnyLanguage()
    {
        Assert.Equal(["año", "día_1", "naïve-tag"], NoteValidator.Tags(["Año", "DÍA_1", "naïve-tag"]));
    }

    [Fact]
    public void AQueryIsGivenBackWithItsTagInLowercaseAndItsTextTrimmed()
    {
        NoteQuery accepted = NoteValidator.Query(new NoteQuery(Tag: "WORK", Text: "  bread  "));

        Assert.Equal("work", accepted.Tag);
        Assert.Equal("bread", accepted.Text);
    }

    [Theory]
    [InlineData(1L, false, null)]
    [InlineData(null, true, null)]
    [InlineData(null, false, "work")]
    public void TheTrashIsNotFilteredByFolderOrTag(long? folderId, bool withoutFolder, string? tag)
    {
        var query = new NoteQuery(FolderId: folderId, WithoutFolder: withoutFolder, Tag: tag, InTrash: true);

        Assert.Equal("inTrash", Assert.Throws<ValidationException>(() => NoteValidator.Query(query)).Field);
    }

    [Fact]
    public void ThePasswordGivenToOpenAProfileIsTakenAsItIs()
    {
        Assert.Equal(" short ", ProfileValidator.GivenPassword(" short "));
        Assert.Equal("password", Assert.Throws<ValidationException>(() => ProfileValidator.GivenPassword("")).Field);
    }

    [Fact]
    public void ANewPasswordKeepsItsSpaces()
    {
        Assert.Equal("  eight  ", ProfileValidator.NewPassword("  eight  "));
    }

    [Fact]
    public void TheKindOfAFileIsTakenInLowercase()
    {
        Assert.Equal("image/png", AttachmentValidator.MimeType(" IMAGE/PNG "));
    }

    [Theory]
    [InlineData("cat.PNG", "image/png")]
    [InlineData("cat.jpg", "image/jpeg")]
    [InlineData("cat.jpeg", "image/jpeg")]
    [InlineData("cat.gif", "image/gif")]
    [InlineData("cat.webp", "image/webp")]
    [InlineData("report.pdf", "application/pdf")]
    [InlineData("list.txt", "text/plain")]
    public void TheKindOfAFileIsToldByTheEndingOfItsName(string fileName, string mimeType)
    {
        Assert.Equal(mimeType, AttachmentValidator.MimeTypeOf(fileName));
    }

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("notes")]
    [InlineData("page.html")]
    public void AFileWhoseNameEndsInAKindNotAcceptedIsRefused(string fileName)
    {
        Assert.Equal("mimeType", Assert.Throws<ValidationException>(() => AttachmentValidator.MimeTypeOf(fileName)).Field);
    }

    [Fact]
    public void NoThemeMeansTheOneOfWindows()
    {
        Assert.Null(SettingsValidator.Settings(ProfileSettings.Default with { Theme = null }).Theme);
    }
}

