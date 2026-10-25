using VanillaNotes.Core.Markdown;
using Xunit;

namespace VanillaNotes.Core.Tests.Markdown;

/// <summary>A note as a safe web page: formatted, and with nothing that can run or reach the network.</summary>
public sealed class MarkdownHtmlWriterTests
{
    private static string Write(string markdown, Func<long, string?>? images = null) => MarkdownHtmlWriter.Write(markdown, images ?? (_ => null));

    [Fact]
    public void TheMarkdownOfTheWebVersionIsFormatted()
    {
        string html = Write("# Title\n\n**bold** ~~gone~~\n\n- [x] done\n\n| a | b |\n|---|---|\n| 1 | 2 |");

        Assert.Contains("<h1", html, StringComparison.Ordinal);
        Assert.Contains("<strong>bold</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<del>gone</del>", html, StringComparison.Ordinal);
        Assert.Contains("type=\"checkbox\"", html, StringComparison.Ordinal);
        Assert.Contains("<table>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlWrittenInANoteIsShownAsText()
    {
        string html = Write("<script>alert(1)</script>\n\nA <b>word</b>");

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[link](javascript:alert(1))")]
    [InlineData("[link](file:///C:/Windows)")]
    [InlineData("[link](vbscript:x)")]
    [InlineData("[link](/relative/path)")]
    public void ALinkKeepsOnlyAWebOrMailAddress(string markdown)
    {
        string html = Write(markdown);

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
        Assert.Contains("link", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[web](https://example.com/page)", "https://example.com/page")]
    [InlineData("[mail](mailto:ana@example.com)", "mailto:ana@example.com")]
    [InlineData("<https://example.com>", "https://example.com")]
    [InlineData("<ana@example.com>", "mailto:ana@example.com")]
    public void AWebOrMailAddressIsKeptAsALink(string markdown, string address)
    {
        Assert.Contains($"href=\"{address}\"", Write(markdown), StringComparison.Ordinal);
    }

    [Fact]
    public void AnImageFromTheNetworkIsNeverFetched()
    {
        string html = Write("![a cat](https://example.com/cat.png)");

        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", html, StringComparison.Ordinal);
        Assert.Contains("a cat", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAttachedImageIsShownFromInsideThePage()
    {
        string html = Write("![a cat](attachment:7)", id => id == 7 ? "data:image/png;base64,AAAA" : null);

        Assert.Contains("<img src=\"data:image/png;base64,AAAA\" alt=\"a cat\" />", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAttachedImageThatCannotBeShownLeavesItsText()
    {
        string html = Write("![a cat](attachment:7)");

        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.Contains("a cat", html, StringComparison.Ordinal);
    }

    [Fact]
    public void CodeOfALanguageTheWebVersionKnowsIsColored()
    {
        string html = Write("```js\n// note\nconst a = \"x\" + 1;\n```");

        Assert.Contains("class=\"language-js\"", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"token token--comment\">// note</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"token token--keyword\">const</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"token token--string\">&quot;x&quot;</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"token token--number\">1</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void CodeOfALanguageItDoesNotKnowIsWrittenPlainAndEscaped()
    {
        string html = Write("```rust\nfn main() { let x = \"<b>\"; }\n```");

        Assert.DoesNotContain("token", html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;", html, StringComparison.Ordinal);
    }
}
