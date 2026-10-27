using VanillaNotes.Core.Models;

namespace VanillaNotes.App.ViewModels;

/// <summary>One tag of the sidebar, and how many notes carry it.</summary>
public sealed class TagItemViewModel
{
    public TagItemViewModel(Tag tag)
    {
        Name = tag.Name;
        NoteCount = tag.NoteCount;
    }

    /// <summary>The tag.</summary>
    public string Name { get; }

    /// <summary>How many notes carry it.</summary>
    public int NoteCount { get; }
}
