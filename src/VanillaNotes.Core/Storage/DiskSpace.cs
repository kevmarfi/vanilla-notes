namespace VanillaNotes.Core.Storage;

/// <summary>
/// Tells whether the disk the data lives on is running out of free space, so that nothing new
/// is stored while what exists can still be read and edited.
/// </summary>
public sealed class DiskSpace
{
    private readonly string _folder;

    private readonly long _minFreeBytes;

    /// <param name="folder">A folder on the disk to watch.</param>
    /// <param name="minFreeBytes">How many bytes must stay free; nothing turns the check off.</param>
    public DiskSpace(string folder, long minFreeBytes)
    {
        _folder = Path.GetFullPath(folder);
        _minFreeBytes = minFreeBytes;
    }

    /// <summary>
    /// Whether the free space of the disk is below what must stay free.
    /// </summary>
    public bool IsLow() => _minFreeBytes > 0 && new DriveInfo(Path.GetPathRoot(_folder)!).AvailableFreeSpace < _minFreeBytes;
}
