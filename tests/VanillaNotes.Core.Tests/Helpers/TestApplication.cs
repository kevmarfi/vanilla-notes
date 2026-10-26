using System.Text;
using VanillaNotes.Core.Data;
using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Storage;

namespace VanillaNotes.Core.Tests.Helpers;

/// <summary>
/// The application with no window, on a data folder made for one test and removed after it:
/// the catalog and the services of the machine, built as the application builds them, and the
/// services of a profile once it is open.
/// </summary>
public sealed class TestApplication : IDisposable
{
    /// <summary>A password long enough to be accepted, for the tests that do not care which.</summary>
    public const string Password = "correct horse battery";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("vanilla-notes-tests-");

    private readonly List<ProfileSession> _sessions = [];

    /// <param name="diskIsFull">Whether the disk of the data is to be taken as running out of space.</param>
    public TestApplication(bool diskIsFull = false)
    {
        DataFolder = new DataFolder(_folder.FullName);
        DataFolder.Prepare();
        Catalog = Database.Open(DataFolder.CatalogPath, Migrator.Catalog);
        Profiles = new ProfileRepository(Catalog);
        DiskSpace = new DiskSpace(_folder.FullName, diskIsFull ? long.MaxValue : 0);
        ProfileService = new ProfileService(Profiles, DataFolder);
        Backups = new BackupService(Catalog, Profiles, DataFolder);
        Maintenance = new MaintenanceService(Profiles, DataFolder, DiskSpace);
    }

    /// <summary>The folder of the data of the test.</summary>
    public DataFolder DataFolder { get; }

    /// <summary>The catalog of the profiles.</summary>
    public Database Catalog { get; }

    /// <summary>The profiles, as the catalog keeps them.</summary>
    public ProfileRepository Profiles { get; }

    /// <summary>The free space of the disk, as the services see it.</summary>
    public DiskSpace DiskSpace { get; }

    /// <summary>The service of the profiles.</summary>
    public ProfileService ProfileService { get; }

    /// <summary>The service of the backups.</summary>
    public BackupService Backups { get; }

    /// <summary>The service of the housekeeping.</summary>
    public MaintenanceService Maintenance { get; }

    /// <summary>
    /// Makes a profile, with a password when one is given, and opens it.
    /// </summary>
    public OpenProfile CreateProfile(string name = "Ana", string? password = null)
    {
        var profile = ProfileService.Create(name);
        OpenProfile open = Open(profile.Id);
        if (password is not null)
        {
            ProfileService.SetPassword(open.Session, password);
        }

        return open;
    }

    /// <summary>
    /// Opens a profile, and builds the services that work on it.
    /// </summary>
    public OpenProfile Open(long profileId, string? password = null)
    {
        ProfileSession session = ProfileService.Open(profileId, password);
        _sessions.Add(session);
        return new OpenProfile(session, DiskSpace);
    }

    /// <summary>Whether a text is in the bytes of a file, or of any file of a folder, as anyone could read them.</summary>
    public static bool DiskHolds(string path, string text)
    {
        byte[] wanted = Encoding.UTF8.GetBytes(text);
        IEnumerable<string> files = Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            : new[] { path, path + "-wal" }.Where(File.Exists);
        return files.Any(file => File.ReadAllBytes(file).AsSpan().IndexOf(wanted) >= 0);
    }

    public void Dispose()
    {
        foreach (ProfileSession session in _sessions)
        {
            session.Dispose();
        }

        Catalog.Dispose();
        _folder.Delete(recursive: true);
    }

    /// <summary>A profile that is open, with the services that work on it.</summary>
    public sealed class OpenProfile
    {
        public OpenProfile(ProfileSession session, DiskSpace diskSpace)
        {
            Session = session;
            var notes = new NoteRepository(session.Database);
            var folders = new FolderRepository(session.Database);
            var attachments = new AttachmentRepository(session.Database);
            Notes = new NoteService(notes, folders, attachments, session, diskSpace);
            Folders = new FolderService(folders);
            Tags = new TagService(new TagRepository(session.Database));
            Settings = new SettingsService(new ProfileSettingsRepository(session.Database));
            Attachments = new AttachmentService(attachments, notes, session, diskSpace);
            Workspace = new WorkspaceService(new WorkspaceRepository(session.Database));
        }

        /// <summary>The open profile itself.</summary>
        public ProfileSession Session { get; }

        /// <summary>Its notes.</summary>
        public NoteService Notes { get; }

        /// <summary>Its folders.</summary>
        public FolderService Folders { get; }

        /// <summary>Its tags.</summary>
        public TagService Tags { get; }

        /// <summary>Its settings.</summary>
        public SettingsService Settings { get; }

        /// <summary>Its attachments.</summary>
        public AttachmentService Attachments { get; }

        /// <summary>Where the person was in its workspace.</summary>
        public WorkspaceService Workspace { get; }

        /// <summary>The number of the profile.</summary>
        public long Id => Session.Profile.Id;
    }
}
