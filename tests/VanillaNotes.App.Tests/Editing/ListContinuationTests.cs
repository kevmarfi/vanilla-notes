using Xunit;
using VanillaNotes.App.Editing;

namespace VanillaNotes.App.Tests.Editing;

/// <summary>What Enter and Tab do on a line of a list or of a quote in the source box.</summary>
public sealed class ListContinuationTests
{
    [Theory]
    [InlineData("- milk", "- milk\n- ")]
    [InlineData("* milk", "* milk\n* ")]
    [InlineData("+ milk", "+ milk\n+ ")]
    [InlineData("1. milk", "1. milk\n2. ")]
    [InlineData("9) milk", "9) milk\n10) ")]
    [InlineData("- [x] milk", "- [x] milk\n- [ ] ")]
    [InlineData("> milk", "> milk\n> ")]
    [InlineData("> - milk", "> - milk\n> - ")]
    [InlineData("    - milk", "    - milk\n    - ")]
    public void EnterAtTheEndOfAnItemStartsTheNextOne(string line, string expected)
    {
        Assert.Equal((expected, expected.Length), ListContinuation.Enter(line, line.Length));
    }

    [Theory]
    [InlineData("- ")]
    [InlineData("1. ")]
    [InlineData("- [ ] ")]
    [InlineData("> ")]
    public void EnterOnAnItemLeftEmptyEndsTheList(string empty)
    {
        string text = "- milk\n" + empty;

        Assert.Equal(("- milk\n", 7), ListContinuation.Enter(text, text.Length));
    }

    [Fact]
    public void EnterInTheMiddleOfAnItemMovesTheRestToTheNextOne()
    {
        Assert.Equal(("- milk\n- bread", 9), ListContinuation.Enter("- milk bread", 6));
    }

    [Fact]
    public void ANewItemEndsItsLineAsTheTextDoes()
    {
        Assert.Equal(("a\r\n- milk\r\n- ", 13), ListContinuation.Enter("a\r\n- milk", 9));
    }

    [Theory]
    [InlineData("plain text", 10)]
    [InlineData("# Heading", 9)]
    [InlineData("---", 3)]
    [InlineData("* * *", 5)]
    [InlineData("-milk", 5)]
    [InlineData("- milk", 1)]
    public void EnterAnywhereElseIsLeftToTheEditor(string text, int caret)
    {
        Assert.Null(ListContinuation.Enter(text, caret));
    }

    [Fact]
    public void TabOnAnItemMovesTheWholeLineIn()
    {
        Assert.Equal(("- milk\n    - bread", 12), ListContinuation.Indent("- milk\n- bread", 8));
    }

    [Fact]
    public void TabAnywhereElseIsLeftToTheEditor()
    {
        Assert.Null(ListContinuation.Indent("plain text", 5));
    }
}
