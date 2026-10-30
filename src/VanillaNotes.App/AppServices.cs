using System.IO;
using VanillaNotes.App.Editing;
using VanillaNotes.App.Services;
using VanillaNotes.App.ViewModels;
using VanillaNotes.Core.Data;
using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Storage;

namespace VanillaNotes.App;

/// <summary>
/// The wiring of the application: it builds every repository, service, and view model, and is the
/// only file that references every folder. What lives as long as the application is built once;
/// what belongs to an open profile is built when the profile opens, over its database.
/// </summary>
public sealed class AppServices : IDisposable
{
    /// <summary>The name of the folder of the data, inside the roaming application data of the Windows account.</summary>
    private const string _dataFolderName = "Vanilla Notes";

    /// <summary>The name of the folder of the record of failures, inside the folder of the application in the local application data.</summary>
    private const string _diagnosticsFolderName = "logs";

    /// <summary>How many bytes of the disk of the data must stay free: one gigabyte, as in the web version.</summary>
    private const long _minFreeBytes = 1024L * 1024 * 1024;

    private readonly DataFolder _dataFolder;

    private readonly Database _catalog;

    private readonly ProfileRepository _profileRepository;

    private readonly DiskSpace _diskSpace;

    private readonly ProfileService _profiles;

    private readonly ThemeService _theme;

    private readonly TextSizeService _textSize;

    private readonly PdfPrinter _printer;

    private readonly BackupService _backups;

    private readonly IdleWatcher _idle;

    private readonly DiagnosticLog _diagnostics;

    private readonly WindowPlacementFile _windowPlacement;

    /// <summary>The profile opened at the start, which the backup of the day copies through its database; nothing when none could be opened.</summary>
    private ProfileSession? _startSession;

    public AppServices(DiagnosticLog diagnostics)
    {
        _diagnostics = diagnostics;
        _windowPlacement = new WindowPlacementFile(LocalFolder);
        _dataFolder = new DataFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), _dataFolderName));
        _dataFolder.Prepare();
        _catalog = Database.Open(_dataFolder.CatalogPath, Migrator.Catalog);
        _profileRepository = new ProfileRepository(_catalog);
        _diskSpace = new DiskSpace(_dataFolder.Root, _minFreeBytes);
        _profiles = new ProfileService(_profileRepository, _dataFolder);
        _backups = new BackupService(_catalog, _profileRepository, _dataFolder);
        new MaintenanceService(_profileRepository, _dataFolder, _diskSpace).RemoveLeftoverProfileFolders();

        Texts = new LanguageService();
        Failures = new FailureMessages(Texts);
        Dialogs = new DialogService(Texts, _windowPlacement.Find, _windowPlacement.Save);
        _theme = new ThemeService();
        _textSize = new TextSizeService();
        _printer = new PdfPrinter();
        _idle = new IdleWatcher();
    }

    /// <summary>The texts of the interface, in its language.</summary>
    public LanguageService Texts { get; }

    /// <summary>The texts of failures.</summary>
    public FailureMessages Failures { get; }

    /// <summary>The windows and dialogs.</summary>
    public DialogService Dialogs { get; }

    /// <summary>
    /// The record of the failures nothing expected. It is kept in the local application data of the
    /// Windows account and not in the folder of the data, so that it stays on this machine and no backup
    /// carries it. It is made before everything else, so that a failure while the rest is built is recorded too.
    /// </summary>
    public static DiagnosticLog CreateDiagnosticLog() => new(Path.Combine(LocalFolder, _diagnosticsFolderName));

    /// <summary>
    /// The window of profiles, in the language and the theme of Windows, since no profile is open.
    /// </summary>
    public ProfileChooserViewModel CreateProfileChooser()
    {
        Texts.Use(LanguageService.WindowsLanguage);
        _theme.Apply(null);
        return new ProfileChooserViewModel(
            _profiles,
            Dialogs,
            Failures,
            _backups,
            Texts,
            () => new NewProfileViewModel(_profiles, Dialogs, Texts, Failures, SettingsOf, RecoveryCode),
            profile => new ProfilePasswordViewModel(profile, _profiles, Dialogs, Texts, Failures, RecoveryCode),
            StartPublicProfile,
            OpenWorkspace);
    }

    /// <summary>
    /// The workspace of the public profile, the one the application starts in; nothing when the
    /// person has not started one, and the window of profiles is shown instead.
    /// </summary>
    public MainViewModel? OpenPublicWorkspace()
    {
        Texts.Use(LanguageService.WindowsLanguage);
        if (_profiles.FindPublic() is not Profile existing)
        {
            return null;
        }

        _startSession = _profiles.Open(existing.Id);
        return OpenWorkspace(_startSession);
    }

    /// <summary>
    /// The workspace of a profile that was just opened, with every service that works on it, in
    /// the language and the theme the profile chose.
    /// </summary>
    public MainViewModel OpenWorkspace(ProfileSession session)
    {
        var notes = new NoteRepository(session.Database);
        var folders = new FolderRepository(session.Database);
        var attachments = new AttachmentRepository(session.Database);
        var noteService = new NoteService(notes, folders, attachments, session, _diskSpace);
        var attachmentService = new AttachmentService(attachments, notes, session, _diskSpace);
        var images = new AttachmentImages(attachmentService.ReadImage);
        var exportService = new ExportService(notes, folders, attachments, session);
        var importService = new ImportService(notes, folders, attachmentService, _diskSpace);
        var folderService = new FolderService(folders);
        var tagService = new TagService(new TagRepository(session.Database));
        SettingsService settings = SettingsOf(session);

        ProfileSettings chosen = settings.Get();
        Texts.Use(chosen.Language);
        _theme.Apply(chosen.Theme);
        _textSize.Apply(chosen.TextSize);

        var editor = new NoteEditorViewModel(noteService, folderService, attachmentService, images.ImageOf, images.Forget, Dialogs, Texts, Failures, _printer, chosen.EditorMode, chosen.Spellcheck);
        return new MainViewModel(
            session,
            new SidebarViewModel(folderService, tagService, Dialogs, Texts, Failures),
            new NoteListViewModel(noteService, Texts, Failures, () => settings.Get().DateStyle, () => settings.Get().NoteOrder),
            editor,
            noteService,
            new WorkspaceService(new WorkspaceRepository(session.Database)),
            _idle,
            chosen.LockAfter,
            _backups,
            Dialogs,
            Texts,
            Failures,
            CreateProfileChooser,
            () => new SettingsViewModel(
                settings, _theme, _textSize, noteService, exportService, _backups, importService, session, _profiles, Dialogs, Texts, Failures, editor.Leave, _diagnostics.Report, RecoveryCode));
    }

    /// <summary>
    /// Makes the backup of the day off the thread of the window, when one is due, through the
    /// profile opened at the start.
    /// </summary>
    public Task BackUpIfDueAsync() => Task.Run(() => _backups.CreateAutomaticIfDue(_startSession));

    public void Dispose()
    {
        _idle.Dispose();
        _catalog.Dispose();
    }

    /// <summary>
    /// The folder of the application in the local application data of the Windows account: what
    /// belongs to this machine and not to the data, which no backup carries.
    /// </summary>
    private static string LocalFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _dataFolderName);

    /// <summary>
    /// Makes the public profile and opens it, when the person chooses to start without a password.
    /// It is named in the language of Windows, which it then takes as its own, and holds a note of
    /// welcome in that language that shows the ways of writing, the keys, and how the notes are
    /// kept safe; the person deletes it as any other.
    /// </summary>
    private ProfileSession StartPublicProfile()
    {
        ProfileSession session = _profiles.Open(_profiles.Create(Texts["profiles.public.name"]).Id);
        SettingsOf(session).Update(ProfileSettings.Default with { Language = LanguageService.WindowsLanguage });
        Database database = session.Database;
        new NoteService(new NoteRepository(database), new FolderRepository(database), new AttachmentRepository(database), session, _diskSpace)
            .Create(Texts["welcome.title"], Texts["welcome.content"]);
        return session;
    }

    private static SettingsService SettingsOf(ProfileSession session) => new(new ProfileSettingsRepository(session.Database));

    private RecoveryCodeViewModel RecoveryCode(string profileName, string code) => new(profileName, code, Dialogs, Texts);
}
