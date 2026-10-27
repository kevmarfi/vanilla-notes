using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VanillaNotes.App.Services;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;

namespace VanillaNotes.App.ViewModels;

/// <summary>
/// The window of profiles, reached from the workspace or when the public profile cannot be opened
/// at the start: the profiles of this machine, opening one, and making a new one; and
/// restoring a backup, which is how the data of another machine, or of a clean data folder, is
/// brought back. Once a profile is open, its workspace takes the place of this page in the window.
/// </summary>
public sealed partial class ProfileChooserViewModel : ObservableObject
{
    private readonly ProfileService _profiles;

    private readonly DialogService _dialogs;

    private readonly FailureMessages _failures;

    private readonly BackupService _backups;

    private readonly LanguageService _texts;

    private readonly Func<NewProfileViewModel> _newProfile;

    private readonly Func<Profile, ProfilePasswordViewModel> _askPassword;

    private readonly Func<ProfileSession, MainViewModel> _openWorkspace;

    /// <summary>What went wrong the last time a profile was opened.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailure))]
    private string _failure = "";

    /// <summary>Whether a backup is being restored, which nothing else may start meanwhile.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand), nameof(CreateCommand), nameof(RestoreBackupCommand))]
    private bool _isRestoring;

    public ProfileChooserViewModel(
        ProfileService profiles,
        DialogService dialogs,
        FailureMessages failures,
        BackupService backups,
        LanguageService texts,
        Func<NewProfileViewModel> newProfile,
        Func<Profile, ProfilePasswordViewModel> askPassword,
        Func<ProfileSession, MainViewModel> openWorkspace)
    {
        _profiles = profiles;
        _dialogs = dialogs;
        _failures = failures;
        _backups = backups;
        _texts = texts;
        _newProfile = newProfile;
        _askPassword = askPassword;
        _openWorkspace = openWorkspace;
        Profiles = new ObservableCollection<Profile>(profiles.List());
    }

    /// <summary>The profiles of this machine, by name.</summary>
    public ObservableCollection<Profile> Profiles { get; }

    /// <summary>Whether something went wrong.</summary>
    public bool HasFailure => Failure.Length > 0;

    /// <summary>Whether nothing is being restored.</summary>
    public bool IsIdle => !IsRestoring;

    /// <summary>Opens a profile, asking for its password when it has one, and goes to its workspace.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void Open(Profile profile)
    {
        Failure = "";
        try
        {
            ProfileSession? session;
            if (profile.HasPassword)
            {
                ProfilePasswordViewModel asked = _askPassword(profile);
                session = _dialogs.ShowDialog(asked) ? asked.Session : null;
            }
            else
            {
                session = _profiles.Open(profile.Id);
            }

            if (session is not null)
            {
                Enter(session);
            }
        }
        catch (AppException failure)
        {
            Failure = _failures.Text(failure);
        }
    }

    /// <summary>Makes a new profile, and goes to its workspace.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void Create()
    {
        NewProfileViewModel created = _newProfile();
        if (_dialogs.ShowDialog(created) && created.Session is ProfileSession session)
        {
            Enter(session);
        }
    }

    /// <summary>
    /// Puts a backup the person picks, from this machine or from anywhere else, in place of all
    /// the data, once the person agrees, off the thread of the window.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task RestoreBackup()
    {
        string? folder = _dialogs.PickFolder(_backups.Folder);
        if (folder is null || !_dialogs.Confirm(_texts.Format("profiles.restore.confirm", ("folder", folder)), "settings.restore.action", isDanger: true))
        {
            return;
        }

        Failure = "";
        IsRestoring = true;
        try
        {
            await Task.Run(() => _backups.RestoreFolder(folder));
            _dialogs.Notify(_texts["settings.restore.done"]);
        }
        catch (AppException failure)
        {
            Failure = _failures.Text(failure);
        }
        finally
        {
            IsRestoring = false;
        }

        Profiles.Clear();
        foreach (Profile profile in _profiles.List())
        {
            Profiles.Add(profile);
        }
    }

    private void Enter(ProfileSession session)
    {
        MainViewModel workspace = _openWorkspace(session);
        _dialogs.Show(workspace, workspace.Leave);
    }
}
