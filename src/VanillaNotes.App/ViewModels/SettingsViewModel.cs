using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VanillaNotes.App.Services;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Export;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Validation;

namespace VanillaNotes.App.ViewModels;

/// <summary>
/// The settings of the open profile: how the interface looks, how notes are written, and how the
/// list of notes shows its dates, each stored in the profile and in effect the moment it is
/// changed; the profile: its name, its password, and deleting it; and the notes and the data:
/// taking every note out, bringing in the notes of the web version, and the backups of this
/// machine; copying the record of failures, for a person who reports a fault; and what the
/// application is: its version, who made it, and the software it is made with.
/// Taking the notes out and making a backup run off the thread of the window. This view model
/// departs from the reference rules by naming the forms of an export, which live in Core/Export.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    /// <summary>The key a setting that is switched on is kept as in its toggle.</summary>
    private const string _on = "on";

    /// <summary>The key a setting that is switched off is kept as in its toggle.</summary>
    private const string _off = "off";

    /// <summary>The start of the name of the file the notes are saved as; the day follows it, as in the web version.</summary>
    private const string _exportFilePrefix = "vanilla-notes-";

    /// <summary>The forms the notes can be taken out in, by the key of their option, which is the one of the web version.</summary>
    private static readonly Dictionary<string, ExportFormat> _exportFormats = new(StringComparer.Ordinal)
    {
        ["markdown"] = ExportFormat.Markdown,
        ["html"] = ExportFormat.Html,
        ["json"] = ExportFormat.Data,
    };

    private readonly SettingsService _settings;

    private readonly ThemeService _themes;

    private readonly TextSizeService _textSizes;

    private readonly DialogService _dialogs;

    private readonly LanguageService _texts;

    private readonly FailureMessages _failures;

    private readonly NoteService _notes;

    private readonly ExportService _export;

    private readonly BackupService _backups;

    private readonly ImportService _import;

    private readonly ProfileSession _session;

    private readonly ProfileService _profiles;

    private readonly Func<bool> _canLeave;

    /// <summary>Gives the report of the record of failures, which lives in a part of Core this view model does not reach.</summary>
    private readonly Func<string> _diagnosticReport;

    private readonly Func<string, string, RecoveryCodeViewModel> _recoveryCode;

    /// <summary>The notices of the software the application is made with, read from inside the application the first time they are shown.</summary>
    private static readonly Lazy<string> _thirdPartyNotices = new(() =>
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("third-party-notices.txt")
            ?? throw new InvalidOperationException("The notices of third parties are not inside the application.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <summary>The settings as they were last stored.</summary>
    private ProfileSettings _stored;

    /// <summary>Whether the names of the options are being given again, which is not the person choosing.</summary>
    private bool _isRelabeling;

    /// <summary>The theme; nothing while the person never chose one, which follows Windows.</summary>
    [ObservableProperty]
    private string? _theme;

    /// <summary>The language of the interface.</summary>
    [ObservableProperty]
    private string _language;

    /// <summary>The size of the text of notes.</summary>
    [ObservableProperty]
    private string _textSize;

    /// <summary>The way of writing a note opens in.</summary>
    [ObservableProperty]
    private string _editorMode;

    /// <summary>Whether the spelling is checked, as the key of its toggle.</summary>
    [ObservableProperty]
    private string _spellcheck;

    /// <summary>How the list of notes shows dates.</summary>
    [ObservableProperty]
    private string _dateStyle;

    /// <summary>The minutes with nothing done after which the profile is closed, or never.</summary>
    [ObservableProperty]
    private string _lockAfter;

    /// <summary>The order of the list of notes, after the ones pinned.</summary>
    [ObservableProperty]
    private string _noteOrder;

    /// <summary>The name typed for the profile.</summary>
    [ObservableProperty]
    private string _profileName;

    /// <summary>Whether the profile has a password, which also means its data is encrypted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoPassword), nameof(CanOfferHello))]
    private bool _hasPassword;

    /// <summary>Whether Windows Hello opens the profile when its password is forgotten.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoHello))]
    private bool _hasHello;

    /// <summary>Whether Windows Hello is set up on this machine.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOfferHello))]
    private bool _isHelloAvailable;

    /// <summary>The form of the profile that is open under its row, if any: setting or changing the password, or deleting the profile.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingPassword), nameof(IsChangingPassword), nameof(IsTurningOnHello), nameof(IsDeleting), nameof(IsFormClosed))]
    private ProfileForm _openForm;

    /// <summary>The password the profile has, typed to change it, to turn Windows Hello on, or to delete the profile.</summary>
    [ObservableProperty]
    private string _currentPassword = "";

    /// <summary>The new password typed.</summary>
    [ObservableProperty]
    private string _newPassword = "";

    /// <summary>The form the notes are taken out in, by the key of its option.</summary>
    [ObservableProperty]
    private string _exportChoice = "markdown";

    /// <summary>Whether the notes are being taken out, or a backup made, which nothing else may start meanwhile.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand), nameof(ImportCommand), nameof(BackUpCommand), nameof(RestoreCommand), nameof(DeleteBackupCommand))]
    private bool _isWorking;

    /// <summary>What came of the last thing done with the notes or the backups.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutcome))]
    private string _outcome = "";

    /// <summary>What went wrong the last time a setting was stored, or something was done with the notes or the backups.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailure))]
    private string _failure = "";

    public SettingsViewModel(
        SettingsService settings,
        ThemeService themes,
        TextSizeService textSizes,
        NoteService notes,
        ExportService export,
        BackupService backups,
        ImportService import,
        ProfileSession session,
        ProfileService profiles,
        DialogService dialogs,
        LanguageService texts,
        FailureMessages failures,
        Func<bool> canLeave,
        Func<string> diagnosticReport,
        Func<string, string, RecoveryCodeViewModel> recoveryCode)
    {
        _profiles = profiles;
        _canLeave = canLeave;
        _diagnosticReport = diagnosticReport;
        _recoveryCode = recoveryCode;
        _profileName = session.Profile.Name;
        _hasPassword = session.Profile.HasPassword;
        _hasHello = session.Profile.HasHello;
        _notes = notes;
        _export = export;
        _backups = backups;
        _import = import;
        _session = session;
        _settings = settings;
        _themes = themes;
        _textSizes = textSizes;
        _dialogs = dialogs;
        _texts = texts;
        _failures = failures;
        _stored = settings.Get();
        _theme = _stored.Theme;
        _language = _stored.Language;
        _textSize = _stored.TextSize;
        _editorMode = _stored.EditorMode;
        _spellcheck = _stored.Spellcheck ? _on : _off;
        _dateStyle = _stored.DateStyle;
        _lockAfter = _stored.LockAfter;
        _noteOrder = _stored.NoteOrder;
        RefreshBackups();
        _ = FindHelloAsync();
    }

    /// <summary>Told with the settings, once one was changed and stored.</summary>
    public event Action<ProfileSettings>? Changed;

    /// <summary>
    /// Told with the name of a backup the person chose to restore, once the settings are closed:
    /// a restore needs the profile closed, which is for the workspace to do.
    /// </summary>
    public event Action<string>? RestoreRequested;

    /// <summary>Told when notes were brought in, so that the workspace lists them.</summary>
    public event Action? Imported;

    /// <summary>Told when the profile was renamed.</summary>
    public event Action? Renamed;

    /// <summary>Told when the profile was deleted, once the settings are closed: the profile is gone, and the workspace goes with it.</summary>
    public event Action? Deleted;

    /// <summary>The forms of the profile that open under its rows.</summary>
    public enum ProfileForm
    {
        /// <summary>No form is open.</summary>
        None,

        /// <summary>Setting a password on the public profile, which turns it private.</summary>
        SetPassword,

        /// <summary>Changing the password, given the current one.</summary>
        ChangePassword,

        /// <summary>Letting Windows Hello open the profile, given its password.</summary>
        TurnOnHello,

        /// <summary>Deleting the profile, given its password when it has one.</summary>
        Delete,
    }

    /// <summary>The themes, each with its name.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Themes => Options(SettingsValidator.Themes, "theme");

    /// <summary>The languages, each named in itself.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Languages { get; } = [new("en", "English"), new("es", "Español")];

    /// <summary>The sizes of the text of notes, each with its name.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> TextSizes => Options(SettingsValidator.TextSizes, "settings.textSize");

    /// <summary>The ways of writing, each with its name.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> EditorModes => Options(SettingsValidator.EditorModes, "editor.mode");

    /// <summary>On and off, for a setting that is switched.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Switch => Options([_on, _off], "settings.switch");

    /// <summary>The ways of showing dates, each with its name.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> DateStyles => Options(SettingsValidator.DateStyles, "settings.dates");

    /// <summary>The orders of the list of notes, each with its name.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> NoteOrders => Options(SettingsValidator.NoteOrders, "settings.order");

    /// <summary>The times with nothing done the profile can be closed after, each with its name.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> LockTimes => Options(SettingsValidator.LockTimes, "settings.lock");

    /// <summary>The forms the notes can be taken out in, each with its name.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> ExportFormats => Options([.. _exportFormats.Keys], "settings.export.format");

    /// <summary>The backups of this machine, newest first.</summary>
    public ObservableCollection<BackupItemViewModel> Backups { get; } = [];

    /// <summary>The version of the application, as its number of three parts.</summary>
    public string Version { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";

    /// <summary>The notices of the software the application is made with, and the full text of their licenses, which travel inside the application.</summary>
    public string ThirdPartyNotices => _thirdPartyNotices.Value;

    /// <summary>Whether the profile has no password.</summary>
    public bool HasNoPassword => !HasPassword;

    /// <summary>Whether the form that sets a password is open.</summary>
    public bool IsSettingPassword => OpenForm == ProfileForm.SetPassword;

    /// <summary>Whether the form that changes the password is open.</summary>
    public bool IsChangingPassword => OpenForm == ProfileForm.ChangePassword;

    /// <summary>Whether the form that turns Windows Hello on is open.</summary>
    public bool IsTurningOnHello => OpenForm == ProfileForm.TurnOnHello;

    /// <summary>Whether Windows Hello can be offered: the profile has a password and Hello is set up on this machine.</summary>
    public bool CanOfferHello => HasPassword && IsHelloAvailable;

    /// <summary>Whether Windows Hello does not open the profile.</summary>
    public bool HasNoHello => !HasHello;

    /// <summary>Whether the form that deletes the profile is open.</summary>
    public bool IsDeleting => OpenForm == ProfileForm.Delete;

    /// <summary>Whether no form of the profile is open, which shows the buttons that open them.</summary>
    public bool IsFormClosed => OpenForm == ProfileForm.None;

    /// <summary>Whether nothing is being done with the notes or the backups.</summary>
    public bool IsIdle => !IsWorking;

    /// <summary>Whether something came of the last thing done.</summary>
    public bool HasOutcome => Outcome.Length > 0;

    /// <summary>Whether something went wrong.</summary>
    public bool HasFailure => Failure.Length > 0;

    /// <summary>Closes the settings.</summary>
    [RelayCommand]
    private void Done() => _dialogs.Close(this);

    // -- Profile -- //

    /// <summary>Gives the profile the name typed.</summary>
    [RelayCommand]
    private void Rename() => Attempt(() =>
    {
        ProfileName = _profiles.Rename(_session, ProfileName).Name;
        Renamed?.Invoke();
        Outcome = _texts["settings.name.changed"];
    });

    /// <summary>Opens a form of the profile, with nothing typed in it.</summary>
    [RelayCommand]
    private void OpenProfileForm(ProfileForm form)
    {
        CurrentPassword = "";
        NewPassword = "";
        Failure = "";
        Outcome = "";
        OpenForm = form;
    }

    /// <summary>Closes the form of the profile, forgetting what was typed.</summary>
    [RelayCommand]
    private void CloseProfileForm() => OpenProfileForm(ProfileForm.None);

    /// <summary>Sets a password on the public profile, which turns it private and encrypts its data, and shows its recovery code once.</summary>
    [RelayCommand]
    private void SetPassword() => Attempt(() =>
    {
        string code = _profiles.SetPassword(_session, NewPassword);
        CloseProfileForm();
        HasPassword = true;
        _dialogs.ShowDialog(_recoveryCode(_session.Profile.Name, code));
        Outcome = _texts["settings.password.setDone"];
    });

    /// <summary>Changes the password, given the current one.</summary>
    [RelayCommand]
    private void ChangePassword() => Attempt(() =>
    {
        _profiles.ChangePassword(_session, CurrentPassword, NewPassword);
        CloseProfileForm();
        Outcome = _texts["settings.password.changed"];
    });

    /// <summary>Lets Windows Hello open the profile when its password is forgotten, given the password; Hello asks the person who they are.</summary>
    [RelayCommand]
    private async Task TurnOnHello()
    {
        Failure = "";
        Outcome = "";
        try
        {
            await _profiles.TurnOnHelloAsync(_session, CurrentPassword);
        }
        catch (AppException failure)
        {
            Failure = _failures.Text(failure);
            return;
        }

        CloseProfileForm();
        HasHello = true;
        Outcome = _texts["settings.hello.turnedOn"];
    }

    /// <summary>Stops Windows Hello from opening the profile.</summary>
    [RelayCommand]
    private void TurnOffHello() => Attempt(() =>
    {
        _profiles.TurnOffHello(_session);
        HasHello = false;
        Outcome = _texts["settings.hello.turnedOff"];
    });

    /// <summary>
    /// Deletes the profile with everything it has, given its password when it has one, once what
    /// was typed in the open note is saved or let go.
    /// </summary>
    [RelayCommand]
    private void DeleteProfile()
    {
        if (!_canLeave())
        {
            return;
        }

        Attempt(() =>
        {
            _profiles.Delete(_session, HasPassword ? CurrentPassword : null);
            CurrentPassword = "";
            _dialogs.Close(this);
            Deleted?.Invoke();
        });
    }

    /// <summary>Does something to the profile, and shows its failure by its code.</summary>
    private void Attempt(Action action)
    {
        Failure = "";
        Outcome = "";
        try
        {
            action();
        }
        catch (AppException failure)
        {
            Failure = _failures.Text(failure);
        }
    }

    private async Task FindHelloAsync() => IsHelloAvailable = await _profiles.IsHelloAvailableAsync();

    // -- Notes and data -- //

    /// <summary>Takes every note of the profile out, in the form chosen, and saves it where the person picks.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task Export()
    {
        Failure = "";
        if (_notes.List(new NoteQuery(Limit: 1)).Total == 0)
        {
            Outcome = _texts["settings.export.empty"];
            return;
        }

        ExportFormat format = _exportFormats[ExportChoice];
        await Work("settings.export.working", async () =>
        {
            byte[] file = await Task.Run(() => _export.Export(format));
            string name = $"{_exportFilePrefix}{DateTime.Now:yyyy-MM-dd}{NoteExporter.Extension(format)}";
            Outcome = _dialogs.SaveFile(name, file) ? _texts["settings.export.done"] : "";
        });
    }

    /// <summary>Brings in the notes of the data file the web version exports, picked by the person.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task Import()
    {
        DialogService.PickedFile? file = _dialogs.PickFile(_texts["settings.import.kinds"], [".json"], Array.MaxLength - 1);
        if (file is null)
        {
            return;
        }

        await Work("settings.import.working", async () =>
        {
            int count = await Task.Run(() => _import.Import(file.Bytes));
            Imported?.Invoke();
            Outcome = _texts.Count("settings.import.done", count);
        });
    }

    /// <summary>Brings in the notes of a folder of Markdown files the person picks, as other editors of notes leave them.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task ImportFolder()
    {
        string? folder = _dialogs.PickFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        if (folder is null)
        {
            return;
        }

        await Work("settings.import.working", async () =>
        {
            FolderImport outcome = await Task.Run(() => _import.ImportFolder(folder));
            Imported?.Invoke();
            Outcome = _texts.Format("settings.importFolder.done", ("notes", outcome.Notes), ("images", outcome.Images), ("leftOut", outcome.LeftOut));
        });
    }

    /// <summary>Makes a backup of all the data of this machine.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task BackUp() => Work("settings.backup.working", async () =>
    {
        await Task.Run(() => _backups.Create(_session));
        RefreshBackups();
        Outcome = _texts["settings.backup.done"];
    });

    /// <summary>
    /// Puts the record of failures on the clipboard, for the person to paste where they report a fault.
    /// Nothing is sent anywhere.
    /// </summary>
    [RelayCommand]
    private void CopyDiagnostics()
    {
        Failure = "";
        _dialogs.CopyText(_diagnosticReport());
        Outcome = _texts["settings.diagnostics.done"];
    }

    /// <summary>Puts a backup in place of all the data, once the person agrees; the workspace closes the profile and does it.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void Restore(BackupItemViewModel backup)
    {
        if (!_dialogs.Confirm(_texts.Format("settings.restore.confirm", ("date", backup.DateText)), "settings.restore.action", isDanger: true))
        {
            return;
        }

        _dialogs.Close(this);
        RestoreRequested?.Invoke(backup.Name);
    }

    /// <summary>Deletes a backup, once the person agrees.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void DeleteBackup(BackupItemViewModel backup)
    {
        if (!_dialogs.Confirm(_texts.Format("settings.backup.deleteConfirm", ("date", backup.DateText)), "settings.backup.delete", isDanger: true))
        {
            return;
        }

        Failure = "";
        Outcome = "";
        try
        {
            _backups.Delete(backup.Name);
        }
        catch (AppException failure)
        {
            Failure = _failures.Text(failure);
        }

        RefreshBackups();
    }

    /// <summary>Does something slow with the notes or the backups, saying it is under way, and nothing else meanwhile.</summary>
    private async Task Work(string workingKey, Func<Task> work)
    {
        Failure = "";
        Outcome = _texts[workingKey];
        IsWorking = true;
        try
        {
            await work();
        }
        catch (AppException failure)
        {
            Outcome = "";
            Failure = _failures.Text(failure);
        }
        finally
        {
            IsWorking = false;
        }
    }

    private void RefreshBackups()
    {
        Backups.Clear();
        foreach (BackupInfo backup in _backups.List())
        {
            Backups.Add(new BackupItemViewModel(backup, _texts));
        }
    }

    // -- Settings -- //

    partial void OnThemeChanged(string? value)
    {
        if (_isRelabeling)
        {
            return;
        }

        if (Store(_stored with { Theme = value }))
        {
            _themes.Apply(value);
        }
    }

    partial void OnLanguageChanged(string value)
    {
        if (Store(_stored with { Language = value }))
        {
            _texts.Use(value);
            Relabel();
        }
    }

    partial void OnTextSizeChanged(string value)
    {
        if (_isRelabeling)
        {
            return;
        }

        if (Store(_stored with { TextSize = value }))
        {
            _textSizes.Apply(value);
        }
    }

    partial void OnEditorModeChanged(string value)
    {
        if (!_isRelabeling)
        {
            Store(_stored with { EditorMode = value });
        }
    }

    partial void OnSpellcheckChanged(string value)
    {
        if (!_isRelabeling)
        {
            Store(_stored with { Spellcheck = value == _on });
        }
    }

    partial void OnDateStyleChanged(string value)
    {
        if (!_isRelabeling)
        {
            Store(_stored with { DateStyle = value });
        }
    }

    partial void OnNoteOrderChanged(string value)
    {
        if (!_isRelabeling)
        {
            Store(_stored with { NoteOrder = value });
        }
    }

    partial void OnLockAfterChanged(string value)
    {
        if (!_isRelabeling)
        {
            Store(_stored with { LockAfter = value });
        }
    }

    /// <summary>
    /// Gives the options their names in the language just chosen. A row of options whose list is
    /// replaced can lose its choice on the way, so each choice is put back as it was stored.
    /// </summary>
    private void Relabel()
    {
        _isRelabeling = true;
        OnPropertyChanged(nameof(Themes));
        OnPropertyChanged(nameof(TextSizes));
        OnPropertyChanged(nameof(EditorModes));
        OnPropertyChanged(nameof(Switch));
        OnPropertyChanged(nameof(DateStyles));
        OnPropertyChanged(nameof(LockTimes));
        OnPropertyChanged(nameof(NoteOrders));
        OnPropertyChanged(nameof(ExportFormats));
        string exportChoice = ExportChoice;
        Theme = _stored.Theme;
        TextSize = _stored.TextSize;
        EditorMode = _stored.EditorMode;
        Spellcheck = _stored.Spellcheck ? _on : _off;
        DateStyle = _stored.DateStyle;
        LockAfter = _stored.LockAfter;
        NoteOrder = _stored.NoteOrder;
        ExportChoice = exportChoice;
        _isRelabeling = false;
        RefreshBackups();
    }

    /// <returns>Whether the settings were stored.</returns>
    private bool Store(ProfileSettings wanted)
    {
        Failure = "";
        try
        {
            _stored = _settings.Update(wanted);
        }
        catch (AppException failure)
        {
            Failure = _failures.Text(failure);
            return false;
        }

        Changed?.Invoke(_stored);
        return true;
    }

    /// <summary>The values of a setting, each with the text of its key after a prefix.</summary>
    private List<KeyValuePair<string, string>> Options(IReadOnlyList<string> values, string keyPrefix) =>
        values.Select(value => new KeyValuePair<string, string>(value, _texts[$"{keyPrefix}.{value}"])).ToList();
}
