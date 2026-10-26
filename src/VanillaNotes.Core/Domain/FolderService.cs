using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Validation;

namespace VanillaNotes.Core.Domain;

/// <summary>The folders of the open profile: listing, making, renaming, and deleting them.</summary>
public sealed class FolderService
{
    private readonly FolderRepository _folders;

    public FolderService(FolderRepository folders)
    {
        _folders = folders;
    }

    /// <summary>Every folder, in the order of their names, each with how many notes it holds.</summary>
    public IReadOnlyList<Folder> List() => _folders.List();

    /// <summary>Makes a folder.</summary>
    public Folder Create(string name) => _folders.Create(FolderValidator.Name(name)) ?? throw NameTaken();

    /// <summary>Gives a folder another name.</summary>
    public Folder Rename(long id, string name)
    {
        string accepted = FolderValidator.Name(name);
        if (_folders.Find(id) is null)
        {
            throw NotFound();
        }

        return _folders.Rename(id, accepted) ?? throw NameTaken();
    }

    /// <summary>Deletes a folder. Its notes are not deleted: they are left in no folder.</summary>
    public void Remove(long id)
    {
        if (!_folders.Remove(id))
        {
            throw NotFound();
        }
    }

    private static ConflictException NameTaken() => new(ErrorCodes.FolderExists, "A folder with that name already exists");

    private static NotFoundException NotFound() => new("Folder not found");
}
