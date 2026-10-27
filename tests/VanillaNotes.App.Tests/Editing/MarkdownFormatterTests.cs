using Xunit;
using VanillaNotes.App.Editing;

namespace VanillaNotes.App.Tests.Editing;

/// <summary>The symbols of a format written into the Markdown source, as markdown-format.js of the web version writes them.</summary>
public sealed class MarkdownFormatterTests
{
    [Theory]
    [InlineData("bold", "a **word** here")]
    [InlineData("italic", "a *word* here")]
    [InlineData("strike", "a ~~word~~ here")]
    public void AWrapperGoesAroundWhatIsSelected(string format, string expected)
    {
        (string text, int start, int length) = MarkdownFormatter.Apply("a word here", 2, 4, format);

        Assert.Equal(expected, text);
        Assert.Equal(expected.IndexOf(" here", StringComparison.Ordinal), start);
        Assert.Equal(0, length);
    }

    [Fact]
    public void WithNothingSelectedTheCursorGoesBetweenTheSymbols()
    {
        (string text, int start, _) = MarkdownFormatter.Apply("ab", 1, 0, "bold");

        Assert.Equal("a****b", text);
        Assert.Equal(3, start);
    }

    [Fact]
    public void ALineFormatReachesEveryLineSelectedAndReplacesTheMarkTheyHad()
    {
        (string text, _, _) = MarkdownFormatter.Apply("# one\n- two\nthree", 3, 9, "blockquote");

        Assert.Equal("> one\n> two\n> three", text);
    }

    [Fact]
    public void ANumberedListCountsItsLines()
    {
        (string text, _, _) = MarkdownFormatter.Apply("one\ntwo\nthree", 0, 13, "orderedList");

        Assert.Equal("1. one\n2. two\n3. three", text);
    }

    [Fact]
    public void AParagraphTakesTheMarksAway()
    {
        (string text, _, _) = MarkdownFormatter.Apply("- [ ] task\n## title", 0, 18, "paragraph");

        Assert.Equal("task\ntitle", text);
    }

    [Fact]
    public void ATaskListMarksEveryLine()
    {
        (string text, _, _) = MarkdownFormatter.Apply("milk\nbread", 0, 10, "taskList");

        Assert.Equal("- [ ] milk\n- [ ] bread", text);
    }

    [Fact]
    public void ABlockOfCodeFencesWhatIsSelected()
    {
        (string text, int start, _) = MarkdownFormatter.Apply("x", 0, 0, "codeBlock");

        Assert.Equal("```\n\n```x", text);
        Assert.Equal(4, start);
    }

    [Fact]
    public void ADividingLineGoesAfterTheSelectionInAParagraphOfItsOwn()
    {
        (string text, _, _) = MarkdownFormatter.Apply("ab", 0, 1, "horizontalRule");

        Assert.Equal("a\n\n---\n\nb", text);
    }

    [Fact]
    public void ALinkTakesWhatIsSelectedAsItsText()
    {
        Assert.Equal("[site](https://example.com)", MarkdownFormatter.Apply("site", 0, 4, "link", "https://example.com").Text);
        Assert.Equal("[link](https://example.com)", MarkdownFormatter.Apply("", 0, 0, "link", "https://example.com").Text);
    }

    [Fact]
    public void AnAttachedImageIsWrittenWhereTheCursorIsWithTheCursorAfterIt()
    {
        (string text, int caret) = MarkdownFormatter.InsertImage("ab", 1, 0, "cat.png", 7);

        Assert.Equal("a![cat.png](attachment:7)b", text);
        Assert.Equal(text.Length - 1, caret);
    }

    [Fact]
    public void TheBracketsInTheNameOfAnInsertedImageAreEscaped()
    {
        Assert.Equal(@"![cat \[1\].png](attachment:7)", MarkdownFormatter.InsertImage("selected", 0, 8, "cat [1].png", 7).Text);
    }

    [Fact]
    public void AFormatItDoesNotKnowIsAFault()
    {
        Assert.Throws<ArgumentException>(() => MarkdownFormatter.Apply("a", 0, 1, "underline"));
    }
}
