using System.Text.RegularExpressions;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Validation;

/// <summary>What the title, the text, and the tags of a note accept, and what the list of notes can be asked for.</summary>
public static partial class NoteValidator
{
    /// <summary>The longest a title may be.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>The longest the text of a note may be, in characters.</summary>
    public const int MaxContentLength = 100_000;

    /// <summary>How many tags a note may carry.</summary>
    public const int MaxTags = 10;

    /// <summary>The longest a search text may be.</summary>
    public const int MaxSearchLength = 100;

    /// <summary>The most notes one page of the list may hold.</summary>
    public const int MaxLimit = 100;

    /// <summary>
    /// Accepts a title: 1 to 200 characters once the spaces around it are taken away.
    /// </summary>
    /// <returns>The title without the spaces around it.</returns>
    public static string Title(string? title)
    {
        string trimmed = title?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            throw new ValidationException("title", "Title is required");
        }

        if (trimmed.Length > MaxTitleLength)
        {
            throw new ValidationException("title", $"Title must be at most {MaxTitleLength} characters long");
        }

        return trimmed;
    }

    /// <summary>
    /// Accepts the text of a note: up to 100,000 characters, kept exactly as written.
    /// </summary>
    public static string Content(string? content)
    {
        content ??= "";
        if (content.Length > MaxContentLength)
        {
            throw new ValidationException("content", $"Content must be at most {MaxContentLength} characters long");
        }

        return content;
    }

    /// <summary>
    /// Accepts the tags of a note: at most 10, each of 1 to 30 letters, digits, hyphens, or
    /// underscores, and none twice regardless of case.
    /// </summary>
    /// <returns>The tags in lowercase, in the order given.</returns>
    public static IReadOnlyList<string> Tags(IEnumerable<string>? tags)
    {
        List<string> given = tags?.ToList() ?? [];
        if (given.Count > MaxTags)
        {
            throw new ValidationException("tags", $"A note can have at most {MaxTags} tags");
        }

        if (!given.All(tag => tag is not null && TagPattern().IsMatch(tag)))
        {
            throw new ValidationException("tags", "Each tag must be 1 to 30 letters, digits, hyphens, or underscores");
        }

        List<string> lowered = given.Select(tag => tag.ToLowerInvariant()).ToList();
        if (lowered.Distinct(StringComparer.Ordinal).Count() != lowered.Count)
        {
            throw new ValidationException("tags", "Tags must not repeat");
        }

        return lowered;
    }

    /// <summary>
    /// Accepts what the list of notes is asked for: one way of filtering by folder, a tag of the
    /// same form a note carries, a search text of 1 to 100 characters, and a page of 1 to 100
    /// notes that starts at 0 or later. The trash is one list: it is not filtered by folder or tag.
    /// </summary>
    /// <returns>The same query, with the tag in lowercase and the search text without the spaces around it.</returns>
    public static NoteQuery Query(NoteQuery query)
    {
        if (query.FolderId is not null && query.WithoutFolder)
        {
            throw new ValidationException("folderId", "A list is of one folder or of no folder, not both");
        }

        if (query.InTrash && (query.FolderId is not null || query.WithoutFolder || query.Tag is not null))
        {
            throw new ValidationException("inTrash", "The trash is not filtered by folder or tag");
        }

        string? tag = null;
        if (query.Tag is not null)
        {
            if (!TagPattern().IsMatch(query.Tag))
            {
                throw new ValidationException("tag", "Tag must be 1 to 30 letters, digits, hyphens, or underscores");
            }

            tag = query.Tag.ToLowerInvariant();
        }

        string? text = null;
        if (query.Text is not null)
        {
            text = query.Text.Trim();
            if (text.Length == 0 || text.Length > MaxSearchLength)
            {
                throw new ValidationException("text", $"Search text must be between 1 and {MaxSearchLength} characters long");
            }
        }

        if (query.Limit < 1 || query.Limit > MaxLimit)
        {
            throw new ValidationException("limit", $"Limit must be between 1 and {MaxLimit}");
        }

        if (query.Offset < 0)
        {
            throw new ValidationException("offset", "Offset must be 0 or more");
        }

        if (!SettingsValidator.NoteOrders.Contains(query.Order))
        {
            throw new ValidationException("order", $"Order must be one of: {string.Join(", ", SettingsValidator.NoteOrders)}");
        }

        return query with { Tag = tag, Text = text };
    }

    [GeneratedRegex(@"^[\p{L}\p{N}_-]{1,30}$")]
    private static partial Regex TagPattern();
}
