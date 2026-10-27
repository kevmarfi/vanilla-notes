using System.Windows;
using System.Windows.Documents;
using Xunit;
using VanillaNotes.App.Editing;
using VanillaNotes.App.Tests.Helpers;
using VanillaNotes.Core.Markdown;

namespace VanillaNotes.App.Tests.Editing;

/// <summary>
/// Markdown into the document of the visual editor and back, case by case: what the visual way
/// claims to edit comes back, an edit made through the calls the editor makes is written as
/// Markdown that shows the same, and a note the visual way would damage is told apart.
/// </summary>
public sealed class RoundTripTests
{
    /// <summary>A note written the way the application writes: an untouched round trip gives it back as it is.</summary>
    private const string _canonical = """
        # Round trip

        A paragraph with **bold**, *italic*, ~~struck~~, `inline code`, and a [link](https://example.com/page).

        ## Lists

        - First item
        - Second item
          - Nested item
          - Another nested
        - Third item

        1. One
        2. Two
        3. Three

        - [x] Done
        - [ ] Pending

        ## Table

        | Name | Amount | Note |
        | :-- | --: | --- |
        | Apples | 3 | **fresh** |
        | Pears | 12 | with `code` |

        ## Code

        ```csharp
        var total = 1 + 2;

        Console.WriteLine(total);
        ```

        > A quote with *emphasis*.

        ![A red square](attachment:7)

        ---

        Last line, first part\
        second part after a hard break.

        """;

    private static string Canonical => _canonical.ReplaceLineEndings("\n");

    /// <summary>Turns Markdown into the document, lets a test edit it, and writes it back.</summary>
    private static string Edit(string markdown, Action<FlowDocument> edit)
    {
        string result = "";
        Sta.Run(() =>
        {
            FlowDocument document = MarkdownToDocument.Convert(markdown);
            edit(document);
            result = DocumentToMarkdown.Convert(document);
        });
        return result;
    }

    private static string Untouched(string markdown) => Edit(markdown, _ => { });

    private static bool RoundTrips(string markdown)
    {
        bool result = false;
        Sta.Run(() => result = DocumentToMarkdown.RoundTrips(markdown));
        return result;
    }

    /// <summary>The first stretch of a text in a document, as the editor would select it.</summary>
    private static TextRange Find(FlowDocument document, string text)
    {
        for (TextPointer? position = document.ContentStart; position is not null; position = position.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (position.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text)
            {
                continue;
            }

            int index = position.GetTextInRun(LogicalDirection.Forward).IndexOf(text, StringComparison.Ordinal);
            if (index >= 0)
            {
                TextPointer start = position.GetPositionAtOffset(index)!;
                return new TextRange(start, start.GetPositionAtOffset(text.Length)!);
            }
        }

        throw new InvalidOperationException($"'{text}' is not in the document.");
    }

    private static string Html(string markdown) => MarkdownHtmlWriter.Write(markdown, id => $"#{id}");

    private static string Fold(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // -- Untouched -- //

    [Fact]
    public void ANoteInTheStyleOfTheApplicationComesBackByteForByte()
    {
        Assert.Equal(Canonical, Untouched(Canonical));
    }

    [Fact]
    public void ANoteInTheStyleOfTheApplicationCanBeWrittenVisually()
    {
        Assert.True(RoundTrips(Canonical));
    }

    [Theory]
    [InlineData("Setext heading\n==============\n")]
    [InlineData("* star item\n* another\n")]
    [InlineData("1) paren one\n2) paren two\n")]
    [InlineData("__bold__ and _italic_ and ***both*** and **bold with *italic* inside**.\n")]
    [InlineData("A [reference link][ref].\n\n[ref]: https://example.com/ref\n")]
    [InlineData("<https://example.com> and &copy; entity.\n")]
    [InlineData("Soft\nwrapped paragraph.\n")]
    [InlineData("    indented code\n")]
    [InlineData("~~~\ntilde fence\n~~~\n")]
    [InlineData("Text with <kbd>Ctrl</kbd>, snake_case_word, 2 * 3 * 4, and a \\* literal star.\n")]
    [InlineData("Not a list: 1\\. one, and \\- two, \\# three.\n")]
    public void ANoteInAnotherStyleIsRewrittenButShowsTheSame(string markdown)
    {
        Assert.True(RoundTrips(markdown));
        // A line broken softly reads as a space: white space is compared as one space.
        Assert.Equal(Fold(Html(markdown)), Fold(Html(Untouched(markdown))));
    }

    [Theory]
    [InlineData("A [link with a title](https://example.com \"The title\").\n")]
    [InlineData("- loose item one\n\n- loose item two\n")]
    public void ANoteTheVisualWayWouldChangeIsToldApart(string markdown)
    {
        Assert.False(RoundTrips(markdown));
    }

    // -- Edits -- //

    [Fact]
    public void BoldAppliedToAWordIsWrittenAroundIt()
    {
        string result = Edit(Canonical, document => Find(document, "paragraph").ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold));

        Assert.Contains("A **paragraph** with **bold**", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ItalicAcrossBoldAndPlainTextIsNested()
    {
        string result = Edit(Canonical, document =>
            new TextRange(Find(document, "with").Start, Find(document, "ital").End).ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic));

        string paragraph = result.Split('\n').Single(line => line.StartsWith("A paragraph", StringComparison.Ordinal));
        Assert.Equal("A paragraph *with **bold**, italic*, ~~struck~~, `inline code`, and a [link](https://example.com/page).", paragraph);
    }

    [Fact]
    public void EnterInsideABlockOfCodeKeepsOneBlock()
    {
        // The block is colored: its first word is a run of its own.
        string result = Edit(Canonical, document => Find(document, "var").End.InsertParagraphBreak());

        Assert.Contains("```csharp\nvar\n total = 1 + 2;\n\nConsole.WriteLine(total);\n```", result, StringComparison.Ordinal);
    }

    [Fact]
    public void EnterAtTheEndOfAListItemMakesAnotherItem()
    {
        string result = Edit(Canonical, document => Find(document, "Third item").End.InsertParagraphBreak().InsertTextInRun("Fourth item"));

        Assert.Contains("- Third item\n- Fourth item", result, StringComparison.Ordinal);
    }

    [Fact]
    public void TypingInsideATableCellChangesItsRow()
    {
        string result = Edit(Canonical, document => Find(document, "Apples").End.InsertTextInRun(" and plums"));

        Assert.Contains("| Apples and plums | 3 | **fresh** |", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SymbolsTypedInTextStayText()
    {
        string result = Edit(Canonical, document => Find(document, "Last line").End.InsertTextInRun(" 2*3 a_b [x] ~y~ <b>"));

        Assert.Contains("<p>Last line 2*3 a_b [x] ~y~ &lt;b&gt;, first part<br />", Html(result), StringComparison.Ordinal);
    }

    [Fact]
    public void ALineTypedAsTheStartOfABlockStaysAParagraph()
    {
        string result = Edit("Text\n", document =>
        {
            Find(document, "Text").Start.InsertTextInRun("1. ");
        });

        Assert.Equal("1\\. Text\n", result);
        Assert.Contains("<p>1. Text</p>", Html(result), StringComparison.Ordinal);
    }

    [Fact]
    public void TheAddressOfAnAttachedImageIsKept()
    {
        string result = Edit(Canonical, document => Find(document, "Last line").End.InsertTextInRun("!"));

        Assert.Contains("![A red square](attachment:7)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AnImageWithBracketsInItsTextComesBackAsItWas()
    {
        const string markdown = "![cat \\[1\\]](attachment:7)\n";

        Assert.Equal(markdown, Untouched(markdown));
    }

    [Fact]
    public void AnImageInsertedWhereTheCursorIsIsWrittenWithTheAddressOfItsAttachment()
    {
        string result = Edit("Before after\n", document => MarkdownToDocument.InsertAttachedImage(Find(document, "after").Start, "cat [1].png", 9, _ => null));

        Assert.Equal("Before ![cat \\[1\\].png](attachment:9)after\n", result);
        Assert.Contains("alt=\"cat [1].png\"", Html(result), StringComparison.Ordinal);
    }
}
