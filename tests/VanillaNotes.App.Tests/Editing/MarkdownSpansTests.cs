using Xunit;
using VanillaNotes.App.Editing;

namespace VanillaNotes.App.Tests.Editing;

/// <summary>The parts of a line of Markdown the source box colors as it is typed.</summary>
public sealed class MarkdownSpansTests
{
    [Fact]
    public void AHeadingIsColoredWithItsHashesAsSymbols()
    {
        (IReadOnlyList<MarkdownSpans.Span> spans, _) = MarkdownSpans.Of("## Plans", inCode: false);

        Assert.Equal([new(0, 8, MarkdownSpans.Kind.Heading), new(0, 2, MarkdownSpans.Kind.Symbol)], spans);
    }

    [Fact]
    public void AHashWithNoSpaceAfterItIsNotAHeading()
    {
        Assert.DoesNotContain(MarkdownSpans.Of("#tag", inCode: false).Spans, span => span.Kind == MarkdownSpans.Kind.Heading);
    }

    [Fact]
    public void AFenceOpensABlockOfCodeAndTheNextOneClosesIt()
    {
        (_, bool afterOpening) = MarkdownSpans.Of("```csharp", inCode: false);
        (IReadOnlyList<MarkdownSpans.Span> inside, bool stillIn) = MarkdownSpans.Of("# not a heading", inCode: afterOpening);
        (_, bool afterClosing) = MarkdownSpans.Of("```", inCode: stillIn);

        Assert.True(afterOpening);
        Assert.Equal([new(0, 15, MarkdownSpans.Kind.Code)], inside);
        Assert.False(afterClosing);
    }

    [Fact]
    public void BoldItalicsAndInlineCodeAreFoundInALine()
    {
        IReadOnlyList<MarkdownSpans.Span> spans = MarkdownSpans.Of("a **b** *c* `d`", inCode: false).Spans;

        Assert.Contains(new MarkdownSpans.Span(2, 5, MarkdownSpans.Kind.Strong), spans);
        Assert.Contains(new MarkdownSpans.Span(8, 3, MarkdownSpans.Kind.Emphasis), spans);
        Assert.Contains(new MarkdownSpans.Span(12, 3, MarkdownSpans.Kind.Code), spans);
    }

    [Fact]
    public void StarsInsideInlineCodeAreNotEmphasis()
    {
        IReadOnlyList<MarkdownSpans.Span> spans = MarkdownSpans.Of("`a *b* c`", inCode: false).Spans;

        Assert.DoesNotContain(spans, span => span.Kind is MarkdownSpans.Kind.Emphasis or MarkdownSpans.Kind.Strong);
    }

    [Fact]
    public void TheAddressOfALinkAndOfAnImageIsColored()
    {
        IReadOnlyList<MarkdownSpans.Span> spans = MarkdownSpans.Of("[site](https://example.com) ![x](attachment:3)", inCode: false).Spans;

        Assert.Contains(new MarkdownSpans.Span(7, 19, MarkdownSpans.Kind.Link), spans);
        Assert.Contains(new MarkdownSpans.Span(33, 12, MarkdownSpans.Kind.Link), spans);
    }

    [Theory]
    [InlineData("- item", 0, 1)]
    [InlineData("  * item", 2, 1)]
    [InlineData("12. item", 0, 3)]
    [InlineData("- [x] done", 0, 5)]
    public void TheMarkOfAnItemOfAListIsColored(string line, int start, int length)
    {
        Assert.Contains(new MarkdownSpans.Span(start, length, MarkdownSpans.Kind.ListMark), MarkdownSpans.Of(line, inCode: false).Spans);
    }

    [Fact]
    public void AQuoteIsColoredWithItsMarkAsASymbol()
    {
        IReadOnlyList<MarkdownSpans.Span> spans = MarkdownSpans.Of("> said", inCode: false).Spans;

        Assert.Equal(new MarkdownSpans.Span(0, 6, MarkdownSpans.Kind.Quote), spans[0]);
        Assert.Equal(new MarkdownSpans.Span(0, 2, MarkdownSpans.Kind.Symbol), spans[1]);
    }
}
