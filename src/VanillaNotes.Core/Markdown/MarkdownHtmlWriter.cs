using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace VanillaNotes.Core.Markdown;

/// <summary>
/// A note as a safe web page, for the export. Nothing in it can run or reach the network: HTML
/// written in the note is shown as text, a link keeps only a web or mail address, and an image
/// is shown only when it is an attached file, carried inside the page. Blocks of code of the
/// languages the web version knows are colored as it colors them.
/// </summary>
public static class MarkdownHtmlWriter
{
    /// <summary>The kinds of address a link may keep.</summary>
    private static readonly string[] _linkSchemes = ["http", "https", "mailto"];

    /// <summary>
    /// Writes the text of a note as the body of a web page.
    /// </summary>
    /// <param name="imageSource">The address that shows an attached image inside the page, by the number of the attachment, or nothing when it cannot be shown.</param>
    public static string Write(string markdown, Func<long, string?> imageSource)
    {
        MarkdownDocument document = MarkdownParser.Parse(markdown);
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        MarkdownParser.Pipeline.Setup(renderer);
        renderer.ObjectRenderers.Replace<LinkInlineRenderer>(new SafeLinkRenderer(imageSource));
        renderer.ObjectRenderers.Replace<AutolinkInlineRenderer>(new SafeAutolinkRenderer());
        renderer.ObjectRenderers.Replace<CodeBlockRenderer>(new ColoredCodeBlockRenderer());
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    /// <summary>Whether an address is one a link may keep: a web or mail address, written in full.</summary>
    private static bool IsAllowedLink(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && _linkSchemes.Contains(uri.Scheme);

    // -- Renderers -- //

    /// <summary>Links that keep only a web or mail address, and images that are only attached files.</summary>
    private sealed class SafeLinkRenderer(Func<long, string?> imageSource) : HtmlObjectRenderer<LinkInline>
    {
        protected override void Write(HtmlRenderer renderer, LinkInline link)
        {
            if (link.IsImage)
            {
                string? source = MarkdownParser.AttachmentId(link.Url) is long id ? imageSource(id) : null;
                if (source is null)
                {
                    // An image that is not an attached file would be fetched from the network: its text stands in for it.
                    renderer.WriteChildren(link);
                    return;
                }

                renderer.Write("<img src=\"").WriteEscapeUrl(source).Write("\" alt=\"");
                renderer.EnableHtmlForInline = false;
                renderer.WriteChildren(link);
                renderer.EnableHtmlForInline = true;
                renderer.Write('"');
                if (!string.IsNullOrEmpty(link.Title))
                {
                    renderer.Write(" title=\"").WriteEscape(link.Title).Write('"');
                }

                renderer.Write(" />");
                return;
            }

            if (!IsAllowedLink(link.Url))
            {
                renderer.WriteChildren(link);
                return;
            }

            renderer.Write("<a href=\"").WriteEscapeUrl(link.Url).Write('"');
            if (!string.IsNullOrEmpty(link.Title))
            {
                renderer.Write(" title=\"").WriteEscape(link.Title).Write('"');
            }

            renderer.Write(">");
            renderer.WriteChildren(link);
            renderer.Write("</a>");
        }
    }

    /// <summary>Addresses written between angle brackets, kept as links only when a link may keep them.</summary>
    private sealed class SafeAutolinkRenderer : HtmlObjectRenderer<AutolinkInline>
    {
        protected override void Write(HtmlRenderer renderer, AutolinkInline link)
        {
            string address = link.IsEmail ? $"mailto:{link.Url}" : link.Url;
            if (!IsAllowedLink(address))
            {
                renderer.WriteEscape(link.Url);
                return;
            }

            renderer.Write("<a href=\"").WriteEscapeUrl(address).Write("\">").WriteEscape(link.Url).Write("</a>");
        }
    }

    /// <summary>Blocks of code, colored by the language they declare when it is one the web version colors.</summary>
    private sealed class ColoredCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
    {
        protected override void Write(HtmlRenderer renderer, CodeBlock block)
        {
            string language = (block as FencedCodeBlock)?.Info?.Trim().ToLowerInvariant() ?? "";
            string code = block.Lines.ToString();
            renderer.EnsureLine();
            renderer.Write("<pre><code");
            if (language.Length > 0)
            {
                renderer.Write(" class=\"language-").WriteEscape(language).Write('"');
            }

            renderer.Write(">");
            CodeColoring.Write(renderer, code, language);
            renderer.Write("</code></pre>");
            renderer.WriteLine();
        }
    }
}
