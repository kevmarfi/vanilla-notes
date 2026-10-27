using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig.Extensions.TaskLists;
using VanillaNotes.Core.Markdown;
using Md = Markdig.Syntax;
using MdInlines = Markdig.Syntax.Inlines;
using MdTables = Markdig.Extensions.Tables;

namespace VanillaNotes.App.Editing;

/// <summary>
/// The text of a note as the document the window shows, formatted: the same document is read in
/// the read way and in the preview, and written in the visual way. What a block is travels in its
/// tag (a heading and its level, a block of code and its language, a quote, a rule); what an
/// inline is travels in its formatting, because that is what the rich-text editor keeps when the
/// person edits. HTML written in the note is text, as the parser of the application reads it.
/// </summary>
public static class MarkdownToDocument
{
    /// <summary>What the tag of a paragraph that is a block of code starts with, before its language.</summary>
    public const string CodeTag = "code:";

    /// <summary>The tag of a section that is a quote.</summary>
    public const string QuoteTag = "quote";

    /// <summary>The tag of a dividing line.</summary>
    public const string RuleTag = "hr";

    /// <summary>What a task that is done starts with, in place of its box.</summary>
    public const string CheckedMark = "☑ ";

    /// <summary>What a task that is not done starts with, in place of its box.</summary>
    public const string UncheckedMark = "☐ ";

    /// <summary>The typeface of code when the window gives none, as in the tests.</summary>
    private const string _fallbackMonoFont = "Cascadia Code, Consolas";

    /// <summary>The size of the text of each level of heading, by the key of its size among the tokens.</summary>
    private static readonly string[] _headingSizes = ["Text2xl", "TextXl", "TextLg", "TextMd", "TextMd", "TextMd"];

    /// <summary>The typeface of code: the one of the tokens, or a fixed one when there are no tokens.</summary>
    public static FontFamily MonoFont =>
        Application.Current?.TryFindResource("FontMono") as FontFamily ?? new FontFamily(_fallbackMonoFont);

    /// <summary>
    /// Turns the text of a note into a document.
    /// </summary>
    /// <param name="imageOf">The picture of an attached image, by the number of the attachment, or nothing when it cannot be shown.</param>
    /// <param name="dark">Whether the document is shown on a dark background, for the colors of the code.</param>
    public static FlowDocument Convert(string markdown, Func<long, ImageSource?>? imageOf = null, bool dark = false)
    {
        var context = new Context(imageOf ?? (_ => null), dark);
        var document = new FlowDocument { PagePadding = new Thickness(0), TextAlignment = TextAlignment.Left };
        document.SetResourceReference(TextElement.FontFamilyProperty, "Font");
        document.SetResourceReference(TextElement.FontSizeProperty, "NoteText");
        document.SetResourceReference(TextElement.ForegroundProperty, "Text");
        foreach (Md.Block block in MarkdownParser.Parse(markdown))
        {
            AddBlock(document.Blocks, block, context);
        }

        return document;
    }

    // -- Blocks the visual editor makes too -- //

    /// <summary>A paragraph of text, with the room under it every paragraph has.</summary>
    public static Paragraph NewParagraph()
    {
        var paragraph = new Paragraph();
        paragraph.SetResourceReference(Block.MarginProperty, "BlockMargin");
        return paragraph;
    }

    /// <summary>Makes a paragraph a heading of a level, from one to six.</summary>
    public static void MakeHeading(Paragraph paragraph, int level)
    {
        paragraph.Tag = $"h{level}";
        paragraph.FontWeight = FontWeights.SemiBold;
        paragraph.SetResourceReference(TextElement.FontSizeProperty, _headingSizes[level - 1]);
        paragraph.SetResourceReference(Block.MarginProperty, "HeadingMargin");
    }

    /// <summary>Makes a paragraph a block of code of a language, or of none.</summary>
    public static void MakeCode(Paragraph paragraph, string language)
    {
        paragraph.Tag = CodeTag + language;
        paragraph.FontFamily = MonoFont;
        paragraph.SetResourceReference(TextElement.FontSizeProperty, "NoteCode");
        paragraph.SetResourceReference(TextElement.BackgroundProperty, "CodeBg");
        paragraph.SetResourceReference(Block.PaddingProperty, "CodeBlockPadding");
        paragraph.SetResourceReference(Block.MarginProperty, "BlockMargin");
    }

    /// <summary>An empty quote: a section with a line on its left, in the muted color.</summary>
    public static Section NewQuote()
    {
        var section = new Section { Tag = QuoteTag };
        section.SetResourceReference(Block.BorderThicknessProperty, "QuoteBorder");
        section.SetResourceReference(Block.PaddingProperty, "QuotePadding");
        section.SetResourceReference(Block.BorderBrushProperty, "Border");
        section.SetResourceReference(TextElement.ForegroundProperty, "TextMuted");
        return section;
    }

    /// <summary>A dividing line.</summary>
    public static BlockUIContainer NewRule()
    {
        var rule = new Separator();
        rule.SetResourceReference(FrameworkElement.MarginProperty, "RuleMargin");
        rule.SetResourceReference(Control.BackgroundProperty, "Border");
        return new BlockUIContainer(rule) { Tag = RuleTag };
    }

    // -- Blocks -- //

    private static void AddBlock(BlockCollection target, Md.Block block, Context context)
    {
        switch (block)
        {
            case Md.HeadingBlock heading:
                var title = new Paragraph();
                MakeHeading(title, heading.Level);
                AddInlines(title.Inlines, heading.Inline, context);
                target.Add(title);
                break;

            case Md.ParagraphBlock paragraph:
                Paragraph text = NewParagraph();
                AddInlines(text.Inlines, paragraph.Inline, context);
                target.Add(text);
                break;

            case Md.ListBlock listBlock:
                target.Add(BuildList(listBlock, context));
                break;

            case Md.QuoteBlock quote:
                Section section = NewQuote();
                foreach (Md.Block child in quote)
                {
                    AddBlock(section.Blocks, child, context);
                }

                target.Add(section);
                break;

            // A fenced block is a code block too: this case covers both, the indented one having no language.
            case Md.CodeBlock code:
                string language = (code as Md.FencedCodeBlock)?.Info ?? "";
                var codeBlock = new Paragraph();
                MakeCode(codeBlock, language);
                codeBlock.Inlines.AddRange(CodeHighlighter.Color(code.Lines.ToString(), language, context.Dark));
                target.Add(codeBlock);
                break;

            case Md.ThematicBreakBlock:
                target.Add(NewRule());
                break;

            case MdTables.Table table:
                target.Add(BuildTable(table, context));
                break;

            case Md.LinkReferenceDefinitionGroup:
                break;

            default:
                // Anything else the parser gives is kept as its text, never dropped.
                target.Add(new Paragraph(new Run(block.ToString() ?? "")));
                break;
        }
    }

    private static List BuildList(Md.ListBlock source, Context context)
    {
        var list = new List
        {
            MarkerStyle = source.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
        };
        list.SetResourceReference(Block.MarginProperty, "BlockMargin");
        list.SetResourceReference(Block.PaddingProperty, "ListPadding");
        if (source.IsOrdered && int.TryParse(source.OrderedStart, out int start))
        {
            list.StartIndex = start;
        }

        foreach (Md.Block item in source)
        {
            var listItem = new ListItem();
            foreach (Md.Block child in (Md.ContainerBlock)item)
            {
                AddBlock(listItem.Blocks, child, context);
            }

            // A paragraph of a list item takes no room of its own below it.
            foreach (Paragraph paragraph in listItem.Blocks.OfType<Paragraph>().Where(paragraph => paragraph.Tag is null))
            {
                paragraph.Margin = new Thickness(0);
            }

            list.ListItems.Add(listItem);
        }

        return list;
    }

    private static Table BuildTable(MdTables.Table source, Context context)
    {
        var table = new Table { CellSpacing = 0 };
        table.SetResourceReference(Block.BorderThicknessProperty, "TableBorder");
        table.SetResourceReference(Block.MarginProperty, "BlockMargin");
        table.SetResourceReference(Block.BorderBrushProperty, "Border");
        table.Tag = string.Join(',', source.ColumnDefinitions.Select(column => column.Alignment?.ToString() ?? ""));
        var rows = new TableRowGroup();
        foreach (Md.Block rowBlock in source)
        {
            var sourceRow = (MdTables.TableRow)rowBlock;
            var row = new TableRow();
            if (sourceRow.IsHeader)
            {
                row.FontWeight = FontWeights.SemiBold;
            }

            foreach (Md.Block cellBlock in sourceRow)
            {
                var cell = new TableCell();
                cell.SetResourceReference(TableCell.BorderThicknessProperty, "TableCellBorder");
                cell.SetResourceReference(TableCell.PaddingProperty, "TableCellPadding");
                cell.SetResourceReference(TableCell.BorderBrushProperty, "Border");
                foreach (Md.Block child in (MdTables.TableCell)cellBlock)
                {
                    AddBlock(cell.Blocks, child, context);
                }

                if (cell.Blocks.Count == 0)
                {
                    cell.Blocks.Add(new Paragraph());
                }

                foreach (Paragraph paragraph in cell.Blocks.OfType<Paragraph>())
                {
                    paragraph.Margin = new Thickness(0);
                }

                row.Cells.Add(cell);
            }

            rows.Rows.Add(row);
        }

        table.RowGroups.Add(rows);
        return table;
    }

    // -- Inlines -- //

    private static void AddInlines(InlineCollection target, MdInlines.ContainerInline? container, Context context)
    {
        if (container is null)
        {
            return;
        }

        foreach (MdInlines.Inline inline in container)
        {
            switch (inline)
            {
                case MdInlines.LiteralInline literal:
                    target.Add(new Run(literal.Content.ToString()));
                    break;

                case MdInlines.EmphasisInline emphasis:
                    var span = new Span();
                    if (emphasis.DelimiterChar == '~')
                    {
                        span.TextDecorations = TextDecorations.Strikethrough;
                    }
                    else if (emphasis.DelimiterCount >= 2)
                    {
                        span.FontWeight = FontWeights.Bold;
                    }
                    else
                    {
                        span.FontStyle = FontStyles.Italic;
                    }

                    AddInlines(span.Inlines, emphasis, context);
                    target.Add(span);
                    break;

                case MdInlines.CodeInline code:
                    var run = new Run(code.Content) { FontFamily = MonoFont };
                    run.SetResourceReference(TextElement.BackgroundProperty, "CodeBg");
                    target.Add(run);
                    break;

                case MdInlines.LinkInline { IsImage: true } image:
                    target.Add(BuildImage(image, context));
                    break;

                case MdInlines.LinkInline link:
                    var hyperlink = new Hyperlink { NavigateUri = new Uri(link.Url ?? "", UriKind.RelativeOrAbsolute), ToolTip = link.Url };
                    hyperlink.SetResourceReference(TextElement.ForegroundProperty, "Accent");
                    AddInlines(hyperlink.Inlines, link, context);
                    target.Add(hyperlink);
                    break;

                case MdInlines.AutolinkInline autolink:
                    string address = autolink.IsEmail ? $"mailto:{autolink.Url}" : autolink.Url;
                    var auto = new Hyperlink(new Run(autolink.Url)) { NavigateUri = new Uri(address, UriKind.RelativeOrAbsolute) };
                    auto.SetResourceReference(TextElement.ForegroundProperty, "Accent");
                    target.Add(auto);
                    break;

                case MdInlines.LineBreakInline lineBreak:
                    target.Add(lineBreak.IsHard ? new LineBreak() : new Run(" "));
                    break;

                case MdInlines.HtmlEntityInline entity:
                    target.Add(new Run(entity.Transcoded.ToString()));
                    break;

                case TaskList task:
                    target.Add(new Run((task.Checked ? CheckedMark : UncheckedMark).TrimEnd()));
                    break;

                case MdInlines.ContainerInline other:
                    AddInlines(target, other, context);
                    break;

                default:
                    target.Add(new Run(inline.ToString() ?? ""));
                    break;
            }
        }
    }

    /// <summary>
    /// An image: the picture when it is an attached file that can be shown, and its alternative
    /// text when it is not. Its text and its address are kept on it either way, since the picture
    /// says neither.
    /// </summary>
    private static InlineUIContainer BuildImage(MdInlines.LinkInline image, Context context)
    {
        string alt = string.Concat(Md.MarkdownObjectExtensions.Descendants<MdInlines.LiteralInline>(image).Select(literal => literal.Content.ToString()));
        return new InlineUIContainer(NewImage(alt, image.Url, context.ImageOf)) { Tag = ImageTag(alt, image.Url) };
    }

    /// <summary>
    /// What an image shows: the picture when it is an attached file that can be shown, and its
    /// alternative text between brackets when it is not.
    /// </summary>
    /// <param name="imageOf">The picture of an attached image, by the number of the attachment, or nothing when it cannot be shown.</param>
    public static UIElement NewImage(string alt, string? url, Func<long, ImageSource?> imageOf)
    {
        ImageSource? picture = MarkdownParser.AttachmentId(url) is long id ? imageOf(id) : null;
        UIElement shown;
        if (picture is not null)
        {
            var picturedImage = new Image { Source = picture, Stretch = Stretch.Uniform, ToolTip = alt };
            picturedImage.SetResourceReference(FrameworkElement.MaxWidthProperty, "NoteImageMaxWidth");
            shown = picturedImage;
        }
        else
        {
            var placeholder = new TextBlock { Text = $"[{alt}]", FontStyle = FontStyles.Italic };
            placeholder.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            shown = placeholder;
        }

        return shown;
    }

    /// <summary>Puts an attached image in the document of the editor, where the cursor is.</summary>
    /// <param name="alt">Its alternative text: the name of the file.</param>
    /// <param name="imageOf">The picture of an attached image, by the number of the attachment.</param>
    public static void InsertAttachedImage(TextPointer at, string alt, long attachmentId, Func<long, ImageSource?> imageOf)
    {
        string url = MarkdownParser.AttachmentAddress(attachmentId);
        _ = new InlineUIContainer(NewImage(alt, url, imageOf), at) { Tag = ImageTag(alt, url) };
    }

    /// <summary>What an image keeps on it, since the picture says neither: its alternative text and its address, on two lines.</summary>
    public static string ImageTag(string alt, string? url) => $"{alt}\n{url}";

    /// <summary>What every block and inline of one conversion needs to know.</summary>
    private sealed record Context(Func<long, ImageSource?> ImageOf, bool Dark);
}
