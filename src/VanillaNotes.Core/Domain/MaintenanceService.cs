using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Storage;

namespace VanillaNotes.Core.Domain;

/// <summary>
/// Housekeeping of the data of this machine: what a write cut short may leave behind, and
/// whether the disk is running out of space.
/// </summary>
public sealed class MaintenanceService
{
    private readonly ProfileRepository _profiles;

    private readonly DataFolder _dataFolder;

    private readonly DiskSpace _diskSpace;

    public MaintenanceService(ProfileRepository profiles, DataFolder dataFolder, DiskSpace diskSpace)
    {
        _profiles = profiles;
        _dataFolder = dataFolder;
        _diskSpace = diskSpace;
    }

    /// <summary>
    /// Deletes the folders of profiles the catalog no longer lists: left behind when deleting a
    /// profile was cut short after its row was gone.
    /// </summary>
    /// <returns>How many were deleted.</returns>
    public int RemoveLeftoverProfileFolders()
    {
        var listed = new HashSet<long>(_profiles.List().Select(profile => profile.Id));
        List<long> leftovers = _dataFolder.ListProfileFolders().Where(id => !listed.Contains(id)).ToList();
        foreach (long id in leftovers)
        {
            _dataFolder.RemoveProfileFolder(id);
        }

        return leftovers.Count;
    }

    /// <summary>Whether the disk of the data is running out of space, so that nothing new is stored.</summary>
    public bool IsDiskLow() => _diskSpace.IsLow();
}
