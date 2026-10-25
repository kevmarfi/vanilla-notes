using System.Globalization;

namespace VanillaNotes.Core.Storage;

/// <summary>
/// The folder where the data of the application lives, and every path inside it: the catalog of
/// the profiles, the folder of each profile with its database and its attachments, and the
/// backups. It is the only class that makes, copies, or deletes folders of the data.
/// </summary>
public sealed class DataFolder
{
    /// <summary>The name of the file of the catalog.</summary>
    private const string _catalogFile = "profiles.db";

    /// <summary>The name of the folder that holds one folder per profile.</summary>
    private const string _profilesFolder = "profiles";

    /// <summary>The name of the database inside the folder of a profile.</summary>
    private const string _profileDatabaseFile = "notes.db";

    /// <summary>The name of the folder of attachment files inside the folder of a profile.</summary>
    private const string _uploadsFolder = "uploads";

    /// <summary>The name of the folder that holds one folder per backup.</summary>
    private const string _backupsFolder = "backups";

    /// <summary>The two files SQLite keeps beside a database while it is in use.</summary>
    private static readonly string[] _databaseCompanions = ["-wal", "-shm"];

    public DataFolder(string root)
    {
        Root = Path.GetFullPath(root);
    }

    /// <summary>The folder of the data itself.</summary>
    public string Root { get; }

    /// <summary>The file of the catalog of the profiles.</summary>
    public string CatalogPath => Path.Combine(Root, _catalogFile);

    /// <summary>The folder that holds the backups.</summary>
    public string BackupsFolder => Path.Combine(Root, _backupsFolder);

    /// <summary>The folder of one profile.</summary>
    public string ProfileFolder(long profileId) =>
        Path.Combine(Root, _profilesFolder, profileId.ToString(CultureInfo.InvariantCulture));

    /// <summary>The database of one profile.</summary>
    public string ProfileDatabasePath(long profileId) => Path.Combine(ProfileFolder(profileId), _profileDatabaseFile);

    /// <summary>The folder of the attachment files of one profile.</summary>
    public string UploadsFolder(long profileId) => Path.Combine(ProfileFolder(profileId), _uploadsFolder);

    /// <summary>
    /// Makes the folder of the data, and the folders it always has, when they are not there yet.
    /// </summary>
    public void Prepare()
    {
        Directory.CreateDirectory(Path.Combine(Root, _profilesFolder));
        Directory.CreateDirectory(BackupsFolder);
    }

    /// <summary>
    /// Makes the folder of a new profile, with its folder of attachments.
    /// </summary>
    public void CreateProfileFolder(long profileId) => Directory.CreateDirectory(UploadsFolder(profileId));

    /// <summary>
    /// Deletes the folder of a profile with everything in it. A folder that is not there is not a failure.
    /// </summary>
    public void RemoveProfileFolder(long profileId)
    {
        string folder = ProfileFolder(profileId);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// The profiles that have a folder on disk, whether or not the catalog still lists them.
    /// </summary>
    public IReadOnlyList<long> ListProfileFolders()
    {
        string profiles = Path.Combine(Root, _profilesFolder);
        if (!Directory.Exists(profiles))
        {
            return [];
        }

        return Directory.EnumerateDirectories(profiles)
            .Select(Path.GetFileName)
            .Select(name => long.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out long id) ? id : 0)
            .Where(id => id > 0)
            .Order()
            .ToList();
    }

    // -- Backups -- //

    /// <summary>
    /// The folder of one backup, inside the folder of the backups.
    /// </summary>
    public string BackupFolder(string name)
    {
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("A backup is named by the moment it was made.", nameof(name));
        }

        return Path.Combine(BackupsFolder, name);
    }

    /// <summary>
    /// The backups there are, by the names of their folders, oldest first. A backup that was
    /// being written when the application stopped is not one of them.
    /// </summary>
    public IReadOnlyList<string> ListBackups()
    {
        if (!Directory.Exists(BackupsFolder))
        {
            return [];
        }

        return Directory.EnumerateDirectories(BackupsFolder)
            .Select(path => Path.GetFileName(path)!)
            .Where(name => !name.EndsWith(PartialSuffix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>What the name of a backup ends with while it is being written, until it is complete.</summary>
    public const string PartialSuffix = ".partial";

    /// <summary>
    /// How many bytes the files of a backup take, all together.
    /// </summary>
    public long BackupSize(string name) =>
        new DirectoryInfo(BackupFolder(name)).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);

    /// <summary>
    /// Deletes a backup. A backup that is not there is not a failure.
    /// </summary>
    public void RemoveBackup(string name)
    {
        string folder = BackupFolder(name);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// Copies the files of a profile that is not open into a backup that is being written: its
    /// database, as it is on disk, and its attachment files. A profile that is not open is not
    /// being written to, so the copy is consistent as it is.
    /// </summary>
    /// <param name="partialBackup">The folder of the backup while it is being written.</param>
    public void CopyClosedProfile(long profileId, string partialBackup)
    {
        string target = Path.Combine(partialBackup, _profilesFolder, profileId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(target);
        string database = ProfileDatabasePath(profileId);
        foreach (string suffix in _databaseCompanions.Prepend(""))
        {
            if (File.Exists(database + suffix))
            {
                File.Copy(database + suffix, Path.Combine(target, _profileDatabaseFile + suffix));
            }
        }

        CopyUploads(profileId, partialBackup);
    }

    /// <summary>
    /// Copies the attachment files of a profile into a backup that is being written.
    /// </summary>
    /// <param name="partialBackup">The folder of the backup while it is being written.</param>
    public void CopyUploads(long profileId, string partialBackup)
    {
        string target = Path.Combine(partialBackup, _profilesFolder, profileId.ToString(CultureInfo.InvariantCulture), _uploadsFolder);
        Directory.CreateDirectory(target);
        string source = UploadsFolder(profileId);
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
    }

    /// <summary>
    /// Where the copy of the catalog goes inside a backup that is being written.
    /// </summary>
    public static string CatalogPathIn(string partialBackup) => Path.Combine(partialBackup, _catalogFile);

    /// <summary>
    /// Where the copy of the database of a profile goes inside a backup that is being written;
    /// its folder is made.
    /// </summary>
    public static string ProfileDatabasePathIn(string partialBackup, long profileId)
    {
        string folder = Path.Combine(partialBackup, _profilesFolder, profileId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, _profileDatabaseFile);
    }

    /// <summary>
    /// Makes the folder of a backup that is about to be written, under its partial name.
    /// </summary>
    /// <returns>The folder, to be completed with the name it will have once written.</returns>
    public string StartBackup(string name)
    {
        string partial = BackupFolder(name) + PartialSuffix;
        if (Directory.Exists(partial))
        {
            Directory.Delete(partial, recursive: true);
        }

        Directory.CreateDirectory(partial);
        return partial;
    }

    /// <summary>Whether a folder anywhere holds a backup: a catalog of its own, under a name that is not the one of a backup still being written.</summary>
    public static bool HoldsBackup(string folder) =>
        File.Exists(CatalogPathIn(folder)) && !folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).EndsWith(PartialSuffix, StringComparison.Ordinal);

    /// <summary>
    /// Copies what a backup in a folder anywhere, such as another disk, holds into a backup of
    /// this machine that is being written under its partial name.
    /// </summary>
    public static void CopyBackup(string source, string partialBackup) => CopyFolder(source, partialBackup);

    /// <summary>
    /// Gives a backup that was written completely the name it is listed by.
    /// </summary>
    public void CompleteBackup(string name) => Directory.Move(BackupFolder(name) + PartialSuffix, BackupFolder(name));

    /// <summary>
    /// Puts the data of a backup in place of the data there is: the catalog and the folder of
    /// every profile are replaced, and the backups are left as they are. No database of the data
    /// may be open while it is done.
    /// </summary>
    public void RestoreFrom(string name)
    {
        string backup = BackupFolder(name);
        if (!File.Exists(CatalogPathIn(backup)))
        {
            throw new DirectoryNotFoundException($"There is no backup named {name}.");
        }

        string profiles = Path.Combine(Root, _profilesFolder);
        if (Directory.Exists(profiles))
        {
            Directory.Delete(profiles, recursive: true);
        }

        foreach (string suffix in _databaseCompanions.Prepend(""))
        {
            File.Delete(CatalogPath + suffix);
        }

        File.Copy(CatalogPathIn(backup), CatalogPath);
        CopyFolder(Path.Combine(backup, _profilesFolder), profiles);
    }

    private static void CopyFolder(string source, string target)
    {
        Directory.CreateDirectory(target);
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (string folder in Directory.EnumerateDirectories(source))
        {
            CopyFolder(folder, Path.Combine(target, Path.GetFileName(folder)));
        }
    }
}
