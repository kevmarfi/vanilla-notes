using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VanillaNotes.App.Services;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;

namespace VanillaNotes.App.ViewModels;

/// <summary>
/// Opening a profile that has a password: with Windows Hello, asked at once, when it is on for the
/// profile and set up on this machine; with the password; or, when it is forgotten, with a new
/// password and either Windows Hello or the recovery code, which gives a new recovery code.
/// </summary>
public sealed partial class ProfilePasswordViewModel : ObservableObject
{
    private readonly Profile _profile;

    private readonly ProfileService _profiles;

    private readonly DialogService _dialogs;

    private readonly FailureMessages _failures;

    private readonly Func<string, string, RecoveryCodeViewModel> _recoveryCode;

    /// <summary>The password typed.</summary>
    [ObservableProperty]
    private string _password = "";

    /// <summary>How the person is getting in: with the password, with Windows Hello, or with the recovery code.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAskingPassword), nameof(IsRecovering), nameof(IsRecoveringWithHello), nameof(IsRecoveringWithCode), nameof(CanSwitchToHello))]
    private WayIn _way;

    /// <summary>Whether Windows Hello can open the profile: on for it, and set up on this machine.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSwitchToHello))]
    private bool _canUseHello;

    /// <summary>The recovery code typed.</summary>
    [ObservableProperty]
    private string _code = "";

    /// <summary>The new password typed, to set with the recovery code.</summary>
    [ObservableProperty]
    private string _newPassword = "";

    /// <summary>What went wrong the last time.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailure))]
    private string _failure = "";

    public ProfilePasswordViewModel(
        Profile profile,
        ProfileService profiles,
        DialogService dialogs,
        LanguageService texts,
        FailureMessages failures,
        Func<string, string, RecoveryCodeViewModel> recoveryCode)
    {
        _profile = profile;
        _profiles = profiles;
        _dialogs = dialogs;
        _failures = failures;
        _recoveryCode = recoveryCode;
        Heading = texts.Format("password.heading", ("name", profile.Name));
        RecoverHeading = texts.Format("recover.heading", ("name", profile.Name));
        if (profile.HasHello)
        {
            _ = FindHelloAsync();
        }
    }

    /// <summary>The ways of getting into the profile.</summary>
    public enum WayIn
    {
        /// <summary>With the password.</summary>
        Password,

        /// <summary>With Windows Hello and a new password, the password being forgotten.</summary>
        Hello,

        /// <summary>With the recovery code and a new password, the password being forgotten.</summary>
        RecoveryCode,
    }

    /// <summary>The heading while the password is asked.</summary>
    public string Heading { get; }

    /// <summary>The heading while the recovery code is asked.</summary>
    public string RecoverHeading { get; }

    /// <summary>Whether something went wrong.</summary>
    public bool HasFailure => Failure.Length > 0;

    /// <summary>Whether the password is asked.</summary>
    public bool IsAskingPassword => Way == WayIn.Password;

    /// <summary>Whether the person is getting back in, the password being forgotten.</summary>
    public bool IsRecovering => Way != WayIn.Password;

    /// <summary>Whether the person is getting back in with Windows Hello.</summary>
    public bool IsRecoveringWithHello => Way == WayIn.Hello;

    /// <summary>Whether the person is getting back in with the recovery code.</summary>
    public bool IsRecoveringWithCode => Way == WayIn.RecoveryCode;

    /// <summary>Whether the person, asked for the recovery code, can use Windows Hello instead.</summary>
    public bool CanSwitchToHello => IsRecoveringWithCode && CanUseHello;

    /// <summary>The profile, open, once the dialog is closed with it opened.</summary>
    public ProfileSession? Session { get; private set; }

    /// <summary>
    /// Opens the profile with Windows Hello, which asks the person who they are. Closing the window
    /// of Hello leaves the password to type; when Hello no longer opens the profile, it is said so.
    /// </summary>
    [RelayCommand]
    private async Task OpenWithHello()
    {
        Failure = "";
        try
        {
            Session = await _profiles.OpenWithHelloAsync(_profile.Id);
        }
        catch (HelloException failure) when (failure.Code == ErrorCodes.HelloCanceled)
        {
            return;
        }
        catch (HelloException failure) when (failure.Code == ErrorCodes.HelloKeyLost)
        {
            CanUseHello = false;
            Failure = _failures.Text(failure);
            return;
        }
        catch (AppException failure)
        {
            Failure = _failures.Text(failure);
            return;
        }

        Password = "";
        _dialogs.Close(this, done: true);
    }

    /// <summary>Opens the profile with the password typed.</summary>
    [RelayCommand]
    private void Open() => Attempt(() => _profiles.Open(_profile.Id, Password));

    /// <summary>Sets the new password with the recovery code, shows the new recovery code, and opens the profile.</summary>
    [RelayCommand]
    private void Reset() => Attempt(() =>
    {
        string newCode = _profiles.ResetPassword(_profile.Id, Code, NewPassword);
        _dialogs.ShowDialog(_recoveryCode(_profile.Name, newCode));
        return _profiles.Open(_profile.Id, NewPassword);
    });

    /// <summary>
    /// Sets the new password with Windows Hello, which asks the person who they are, and opens
    /// the profile. When Hello no longer opens it, the recovery code is asked instead.
    /// </summary>
    [RelayCommand]
    private async Task ResetWithHello()
    {
        Failure = "";
        try
        {
            await _profiles.ResetPasswordWithHelloAsync(_profile.Id, NewPassword);
        }
        catch (HelloException failure) when (failure.Code == ErrorCodes.HelloKeyLost)
        {
            CanUseHello = false;
            Way = WayIn.RecoveryCode;
            Failure = _failures.Text(failure);
            return;
        }
        catch (AppException failure)
        {
            Failure = _failures.Text(failure);
            return;
        }

        Attempt(() => _profiles.Open(_profile.Id, NewPassword));
    }

    /// <summary>Asks for a new password with Windows Hello when it can open the profile, or with the recovery code.</summary>
    [RelayCommand]
    private void Forgot() => Go(CanUseHello ? WayIn.Hello : WayIn.RecoveryCode);

    /// <summary>Asks for the recovery code instead of Windows Hello.</summary>
    [RelayCommand]
    private void UseCode() => Go(WayIn.RecoveryCode);

    /// <summary>Asks for Windows Hello instead of the recovery code.</summary>
    [RelayCommand]
    private void UseHello() => Go(WayIn.Hello);

    /// <summary>Asks for the password again.</summary>
    [RelayCommand]
    private void Back() => Go(WayIn.Password);

    /// <summary>Closes the dialog without opening the profile.</summary>
    [RelayCommand]
    private void Cancel() => _dialogs.Close(this);

    private void Attempt(Func<ProfileSession> open)
    {
        Failure = "";
        try
        {
            Session = open();
            Password = "";
            NewPassword = "";
            _dialogs.Close(this, done: true);
        }
        catch (AppException failure)
        {
            Failure = _failures.Text(failure);
        }
    }

    private void Go(WayIn way)
    {
        Failure = "";
        Way = way;
    }

    /// <summary>Asks Windows Hello at once, when it can open the profile.</summary>
    private async Task FindHelloAsync()
    {
        CanUseHello = await _profiles.IsHelloAvailableAsync();
        if (CanUseHello)
        {
            await OpenWithHello();
        }
    }
}
