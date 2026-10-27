using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VanillaNotes.App.Services;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Validation;

namespace VanillaNotes.App.ViewModels;

/// <summary>
/// Making a private profile: its name, its language, its password, which every profile but the
/// public one has, and, where Windows Hello is set up, whether Hello may open it when the password
/// is forgotten. Once made, its recovery code is shown, and the profile is open.
/// </summary>
public sealed partial class NewProfileViewModel : ObservableObject
{
    /// <summary>The option of the switch of Windows Hello that turns it on.</summary>
    private const string _on = "on";

    /// <summary>The option of the switch of Windows Hello that leaves it off.</summary>
    private const string _off = "off";

    private readonly ProfileService _profiles;

    private readonly DialogService _dialogs;

    private readonly FailureMessages _failures;

    private readonly Func<ProfileSession, SettingsService> _settingsOf;

    private readonly Func<string, string, RecoveryCodeViewModel> _recoveryCode;

    /// <summary>The name of the new profile.</summary>
    [ObservableProperty]
    private string _name = "";

    /// <summary>The language of the new profile.</summary>
    [ObservableProperty]
    private string _language = LanguageService.WindowsLanguage;

    /// <summary>The password of the new profile.</summary>
    [ObservableProperty]
    private string _password = "";

    /// <summary>Whether Windows Hello will open the profile when its password is forgotten, by the key of its option.</summary>
    [ObservableProperty]
    private string _hello = _on;

    /// <summary>Whether Windows Hello is set up on this machine, which is when it is offered.</summary>
    [ObservableProperty]
    private bool _isHelloAvailable;

    /// <summary>What went wrong the last time the profile was asked to be made.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailure))]
    private string _failure = "";

    public NewProfileViewModel(
        ProfileService profiles,
        DialogService dialogs,
        LanguageService texts,
        FailureMessages failures,
        Func<ProfileSession, SettingsService> settingsOf,
        Func<string, string, RecoveryCodeViewModel> recoveryCode)
    {
        _profiles = profiles;
        _dialogs = dialogs;
        _failures = failures;
        _settingsOf = settingsOf;
        _recoveryCode = recoveryCode;
        Switch = [new(_on, texts["settings.switch.on"]), new(_off, texts["settings.switch.off"])];
        _ = FindHelloAsync();
    }

    /// <summary>On and off, for the switch of Windows Hello.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Switch { get; }

    /// <summary>The languages the profile can be in, each named in itself.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Languages { get; } = [new("en", "English"), new("es", "Español")];

    /// <summary>Whether something went wrong.</summary>
    public bool HasFailure => Failure.Length > 0;

    /// <summary>The profile made and opened, once the dialog is closed with it made.</summary>
    public ProfileSession? Session { get; private set; }

    /// <summary>
    /// Makes the profile, opens it, and sets its password and its language, and turns Windows
    /// Hello on when it was chosen. A profile made is kept even when Hello is not turned on: that
    /// is said once the recovery code is shown, and Hello can be turned on in the settings.
    /// </summary>
    [RelayCommand]
    private async Task Create()
    {
        Failure = "";
        try
        {
            ProfileValidator.Name(Name);
            ProfileValidator.NewPassword(Password);

            Profile profile = _profiles.Create(Name);
            ProfileSession session = _profiles.Open(profile.Id);
            _settingsOf(session).Update(ProfileSettings.Default with { Language = Language });
            string code = _profiles.SetPassword(session, Password);
            string helloFailure = "";
            if (IsHelloAvailable && Hello == _on)
            {
                try
                {
                    await _profiles.TurnOnHelloAsync(session, Password);
                }
                catch (HelloException failure)
                {
                    helloFailure = _failures.Text(failure);
                }
            }

            _dialogs.ShowDialog(_recoveryCode(profile.Name, code));
            if (helloFailure.Length > 0)
            {
                _dialogs.Notify(helloFailure);
            }

            Password = "";
            Session = session;
            _dialogs.Close(this, done: true);
        }
        catch (AppException failure)
        {
            Failure = _failures.Text(failure);
        }
    }

    /// <summary>Closes the dialog without making a profile.</summary>
    [RelayCommand]
    private void Cancel() => _dialogs.Close(this);

    private async Task FindHelloAsync() => IsHelloAvailable = await _profiles.IsHelloAvailableAsync();
}
