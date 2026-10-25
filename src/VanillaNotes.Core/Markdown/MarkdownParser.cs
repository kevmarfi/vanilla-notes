using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace VanillaNotes.Core.Markdown;

/// <summary>
/// The one parser of the application: which Markdown it understands, the same in the window and
/// in the export. It is the Markdown of the web version: tables, struck-through text, and task
/// lists besides the common forms. HTML written in a note is never taken as HTML: it is text.
/// </summary>
public static partial class MarkdownParser
{
    /// <summary>What the address of an attached file starts with, in the text of a note, before the number of the attachment.</summary>
    public const string AttachmentScheme = "attachment:";

    /// <summary>The way every note is read.</summary>
    public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseTaskLists()
        .Build();

    /// <summary>The way every note is read, keeping where in the text each part is, so that a part can be written over.</summary>
    private static readonly MarkdownPipeline _located = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseTaskLists()
        .UsePreciseSourceLocation()
        .Build();

    /// <summary>
    /// Puts another address in place of the address of every image of a text, as Markdown reads
    /// it: not what only looks like an image inside a block of code.
    /// </summary>
    /// <param name="replacement">The address to use for an image, given the address it has, or nothing to leave it as it is.</param>
    public static string ReplaceImageAddresses(string content, Func<string, string?> replacement)
    {
        MarkdownDocument document = Markdig.Markdown.Parse(content, _located);
        List<LinkInline> images = document.Descendants<LinkInline>()
            .Where(link => link.IsImage && link.Url is not null && !link.UrlSpan.IsEmpty)
            .OrderByDescending(link => link.UrlSpan.Start)
            .ToList();
        string written = content;
        foreach (LinkInline image in images)
        {
            if (replacement(image.Url!) is string address)
            {
                written = string.Concat(written.AsSpan(0, image.UrlSpan.Start), address, written.AsSpan(image.UrlSpan.End + 1));
            }
        }

        return written;
    }

    /// <summary>Reads the text of a note into its parts.</summary>
    public static MarkdownDocument Parse(string markdown) => Markdig.Markdown.Parse(markdown, Pipeline);

    /// <summary>
    /// The address a note uses for one of its attached files.
    /// </summary>
    public static string AttachmentAddress(long attachmentId) => AttachmentScheme + attachmentId.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads the number of an attached file from an address, when the address is the one of an
    /// attached file.
    /// </summary>
    /// <returns>The number of the attachment, or nothing when the address points elsewhere.</returns>
    public static long? AttachmentId(string? address)
    {
        Match match = address is null ? Match.Empty : AttachmentAddressPattern().Match(address);
        return match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long id) ? id : null;
    }

    /// <summary>
    /// The numbers of every attached file the texts of some notes point at, each once.
    /// </summary>
    public static IReadOnlyList<long> FindAttachmentIds(IEnumerable<string> contents) =>
        contents
            .SelectMany(content => AttachmentReferencePattern().Matches(content))
            .Select(match => long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .Distinct()
            .Order()
            .ToList();

    /// <summary>
    /// Puts another address in place of every address of an attached file in the text of a note.
    /// </summary>
    /// <param name="replacement">The address to use for an attachment, or nothing to leave its address as it is.</param>
    public static string ReplaceAttachmentAddresses(string content, Func<long, string?> replacement) =>
        AttachmentReferencePattern().Replace(content, match =>
            replacement(long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)) ?? match.Value);

    [GeneratedRegex("^attachment:([1-9][0-9]{0,17})$")]
    private static partial Regex AttachmentAddressPattern();

    [GeneratedRegex("attachment:([1-9][0-9]{0,17})(?![0-9])")]
    private static partial Regex AttachmentReferencePattern();
}
