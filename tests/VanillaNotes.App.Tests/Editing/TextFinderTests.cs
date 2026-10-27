using Xunit;
using VanillaNotes.App.Editing;

namespace VanillaNotes.App.Tests.Editing;

/// <summary>Finding a text in the Markdown of a note and putting another in its place.</summary>
public sealed class TextSearchTests
{
    [Fact]
    public void TheNextMatchIsFoundFromTheCursorOn()
    {
        Assert.Equal(8, TextFinder.Find("cat dog cat", "cat", 1, matchCase: false));
    }

    [Fact]
    public void PastTheLastMatchTheSearchGoesRoundToTheStart()
    {
        Assert.Equal(0, TextFinder.Find("cat dog cat", "cat", 9, matchCase: false));
    }

    [Fact]
    public void BackwardsTheMatchThatEndsBeforeTheCursorIsFound()
    {
        Assert.Equal(0, TextFinder.Find("cat dog cat", "cat", 8, matchCase: false, backwards: true));
        Assert.Equal(8, TextFinder.Find("cat dog cat", "cat", 0, matchCase: false, backwards: true));
    }

    [Fact]
    public void CapitalsAreToldApartOnlyWhenAsked()
    {
        Assert.Equal(4, TextFinder.Find("Cat cat", "cat", 0, matchCase: true));
        Assert.Equal(0, TextFinder.Find("Cat cat", "cat", 0, matchCase: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bird")]
    public void NothingIsFoundForNothingOrForWhatIsNotThere(string query)
    {
        Assert.Null(TextFinder.Find("cat dog", query, 0, matchCase: false));
    }

    [Fact]
    public void ReplacingAllPutsTheTextInPlaceOfEveryMatchAndCountsThem()
    {
        Assert.Equal(("dog dog dog", 3), TextFinder.ReplaceAll("cat cat Cat", "cat", "dog", matchCase: false));
        Assert.Equal(("dog dog Cat", 2), TextFinder.ReplaceAll("cat cat Cat", "cat", "dog", matchCase: true));
    }

    [Fact]
    public void AReplacementThatHoldsTheTextLookedForIsNotReplacedAgain()
    {
        Assert.Equal(("cats cats", 2), TextFinder.ReplaceAll("cat cat", "cat", "cats", matchCase: false));
    }

    [Fact]
    public void TheSelectionIsAMatchWhenItIsTheTextLookedFor()
    {
        Assert.True(TextFinder.IsMatch("Cat", "cat", matchCase: false));
        Assert.False(TextFinder.IsMatch("Cat", "cat", matchCase: true));
        Assert.False(TextFinder.IsMatch("", "", matchCase: false));
    }
}
