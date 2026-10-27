using VanillaNotes.App.Converters;
using VanillaNotes.App.Services;
using VanillaNotes.Core.Models;

namespace VanillaNotes.App.ViewModels;

/// <summary>One note of the list: its title, when it last changed or, in the trash, when it was deleted, its tags, and, when searching, a fragment of its text.</summary>
public sealed class NoteItemViewModel
{
    public NoteItemViewModel(NoteSummary note, string? snippet, string dateStyle, LanguageService texts)
    {
        Id = note.Id;
        Title = note.Title;
        Tags = note.Tags;
        IsPinned = note.IsPinned;
        Snippet = snippet ?? "";
        DateText = note.DeletedAt is DateTimeOffset deleted
            ? texts.Format("list.deletedAt", ("date", DateTextConverter.Describe(deleted, dateStyle, texts)))
            : DateTextConverter.Describe(note.UpdatedAt, dateStyle, texts);
    }

    /// <summary>The number of the note.</summary>
    public long Id { get; }

    /// <summary>Its title.</summary>
    public string Title { get; }

    /// <summary>Its tags.</summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>Whether it is pinned, and so first in the list.</summary>
    public bool IsPinned { get; }

    /// <summary>When it last changed, or when it was deleted, as the setting of dates says.</summary>
    public string DateText { get; }

    /// <summary>The fragment of its text around the words searched for, or nothing.</summary>
    public string Snippet { get; }

    /// <summary>Whether there is a fragment to show.</summary>
    public bool HasSnippet => Snippet.Length > 0;

    /// <summary>Whether it carries a tag.</summary>
    public bool HasTags => Tags.Count > 0;
}
