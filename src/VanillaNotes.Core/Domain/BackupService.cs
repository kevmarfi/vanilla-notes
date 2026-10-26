using System.Globalization;
using VanillaNotes.Core.Data;
using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Storage;

namespace VanillaNotes.Core.Domain;

/// <summary>
/// Backups of all the data of this machine: a consistent copy of the catalog and of every
/// profile, with its attachment files, in a folder of its own; and putting one back in place of
/// the data there is. The copy of an encrypted profile stays encrypted with its key. Once a day the
/// application makes one on its own, and keeps the seven newest of those; a backup the person made
/// is deleted only by the person.
/// </summary>
public sealed class BackupService
{
    /// <summary>The form the name of a backup takes: the moment it was made, in UTC, with nothing a file name refuses.</summary>
    private const string _nameFormat = "yyyy-MM-ddTHH-mm-ss-fffZ";

    /// <summary>What the name of a backup the application made on its own ends with, after the moment.</summary>
    private const string _automaticSuffix = "-auto";

    /// <summary>How many of the backups the application made on its own are kept: seven, as the web version keeps.</summary>
    public const int AutomaticKept = 7;

    /// <summary>How old the newest backup the application made on its own must be for it to make another: a day.</summary>
    public static readonly TimeSpan AutomaticEvery = TimeSpan.FromDays(1);

    private readonly Database _catalog;

    private readonly ProfileRepository _profiles;

    private readonly DataFolder _dataFolder;

    public BackupService(Database catalog, ProfileRepository profiles, DataFolder dataFolder)
    {
        _catalog = catalog;
        _profiles = profiles;
        _dataFolder = dataFolder;
    }

    /// <summary>Every backup there is, newest first.</summary>
    public IReadOnlyList<BackupInfo> List() =>
        _dataFolder.ListBackups()
            .Reverse()
            .Select(name => new BackupInfo(name, MomentOf(name), _dataFolder.BackupSize(name), IsAutomatic(name)))
            .ToList();

    /// <summary>
    /// Makes a backup of all the data while it is in use. The profile that is open is copied
    /// through its database; the others are not being written, and their files are copied as
    /// they are.
    /// </summary>
    /// <param name="open">The profile that is open, or nothing when none is.</param>
    public BackupInfo Create(ProfileSession? open) => Create(open, automatic: false);

    /// <summary>
    /// Makes the backup of the day on its own, when the newest one the application made is a day
    /// old or there is none; then deletes the ones it made beyond the seven newest. The backups the
    /// person made are neither counted nor deleted.
    /// </summary>
    /// <param name="open">The profile that is open, or nothing when none is.</param>
    /// <returns>The backup made, or nothing when none was due.</returns>
    public BackupInfo? CreateAutomaticIfDue(ProfileSession? open)
    {
        BackupInfo? newest = List().FirstOrDefault(backup => backup.IsAutomatic);
        if (newest is not null && DateTimeOffset.UtcNow - newest.CreatedAt < AutomaticEvery)
        {
            return null;
        }

        BackupInfo made = Create(open, automatic: true);
        foreach (BackupInfo old in List().Where(backup => backup.IsAutomatic).Skip(AutomaticKept))
        {
            _dataFolder.RemoveBackup(old.Name);
        }

        return made;
    }

    private BackupInfo Create(ProfileSession? open, bool automatic)
    {
        DateTimeOffset moment = DateTimeOffset.UtcNow;
        string name = moment.ToString(_nameFormat, CultureInfo.InvariantCulture) + (automatic ? _automaticSuffix : "");
        string partial = _dataFolder.StartBackup(name);
        try
        {
            _catalog.CopyTo(DataFolder.CatalogPathIn(partial));
            foreach (Profile profile in _profiles.List())
            {
                if (open?.Profile.Id == profile.Id)
                {
                    open.Database.CopyTo(DataFolder.ProfileDatabasePathIn(partial, profile.Id));
                    _dataFolder.CopyUploads(profile.Id, partial);
                }
                else
                {
                    _dataFolder.CopyClosedProfile(profile.Id, partial);
                }
            }

            _dataFolder.CompleteBackup(name);
        }
        catch
        {
            Directory.Delete(partial, recursive: true);
            throw;
        }

        return new BackupInfo(name, moment, _dataFolder.BackupSize(name), automatic);
    }

    /// <summary>
    /// Puts a backup in place of all the data there is. A backup of the data as it was is made
    /// first, so that the restore can itself be undone. No profile may be open.
    /// </summary>
    /// <returns>The backup of the data as it was before the restore.</returns>
    public BackupInfo Restore(string name)
    {
        if (!_dataFolder.ListBackups().Contains(name, StringComparer.Ordinal))
        {
            throw new NotFoundException("Backup not found");
        }

        BackupInfo before = Create(open: null);
        _catalog.CloseConnections();
        _dataFolder.RestoreFrom(name);

        // A backup made by an older release is brought up to this one.
        using (var connection = _catalog.Connect())
        {
            Migrator.Apply(connection, Migrator.Catalog);
        }

        return before;
    }

    /// <summary>
    /// Puts the backup in a folder the person picked in place of all the data, as a restore of
    /// one of the backups does. A folder that is not among the backups, such as one moved to
    /// another disk, is copied in first, under its name; one with the name of a backup already
    /// there is taken to be that backup, since a backup is named by the moment it was made, to the
    /// millisecond. No profile may be open.
    /// </summary>
    /// <returns>The backup of the data as it was before the restore.</returns>
    public BackupInfo RestoreFolder(string folder)
    {
        string full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!DataFolder.HoldsBackup(full))
        {
            throw new ValidationException("backup", "The folder is not a backup of Vanilla Notes");
        }

        string name = Path.GetFileName(full);
        if (!_dataFolder.ListBackups().Contains(name, StringComparer.Ordinal))
        {
            string partial = _dataFolder.StartBackup(name);
            try
            {
                DataFolder.CopyBackup(full, partial);
                _dataFolder.CompleteBackup(name);
            }
            catch
            {
                Directory.Delete(partial, recursive: true);
                throw;
            }
        }

        return Restore(name);
    }

    /// <summary>The folder the backups of this machine are kept in, where the person is first shown to pick one.</summary>
    public string Folder => _dataFolder.BackupsFolder;

    /// <summary>Deletes a backup.</summary>
    public void Delete(string name)
    {
        if (!_dataFolder.ListBackups().Contains(name, StringComparer.Ordinal))
        {
            throw new NotFoundException("Backup not found");
        }

        _dataFolder.RemoveBackup(name);
    }

    private static bool IsAutomatic(string name) => name.EndsWith(_automaticSuffix, StringComparison.Ordinal);

    private static DateTimeOffset MomentOf(string name) =>
        DateTimeOffset.TryParseExact(IsAutomatic(name) ? name[..^_automaticSuffix.Length] : name, _nameFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset moment)
            ? moment
            : DateTimeOffset.MinValue;
}
