using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Domain;

/// <summary>The tags of the open profile. A tag is made by putting it on a note, and is gone when no note carries it.</summary>
public sealed class TagService
{
    private readonly TagRepository _tags;

    public TagService(TagRepository tags)
    {
        _tags = tags;
    }

    /// <summary>Every tag some note carries, in alphabetical order, each with how many notes carry it.</summary>
    public IReadOnlyList<Tag> List() => _tags.ListInUse();
}
