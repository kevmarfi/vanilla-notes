using System.Security.Cryptography;
using VanillaNotes.Core.Data;
using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Security;
using VanillaNotes.Core.Storage;
using VanillaNotes.Core.Validation;

namespace VanillaNotes.Core.Domain;

/// <summary>
/// The profiles of this machine: making one, opening it, guarding it with a password, getting
/// back into it with the recovery code or with Windows Hello, renaming it, and deleting it. Setting a password encrypts
/// what the profile has. A password is never removed: the one profile without one is the public
/// profile, the one the application starts in.
/// </summary>
public sealed class ProfileService
{
    private readonly ProfileRepository _profiles;

    private readonly DataFolder _dataFolder;

    public ProfileService(ProfileRepository profiles, DataFolder dataFolder)
    {
        _profiles = profiles;
        _dataFolder = dataFolder;
    }

    /// <summary>Every profile of this machine, in the order of their names.</summary>
    public IReadOnlyList<Profile> List() => _profiles.List();

    /// <summary>
    /// The public profile: the one without a password, or the first that was made when a backup or
    /// an older copy left several; nothing when every profile has a password.
    /// </summary>
    public Profile? FindPublic() => _profiles.List().Where(profile => !profile.HasPassword).MinBy(profile => profile.Id);

    /// <summary>
    /// Makes a profile with no password, with its folder and its empty database. It is the public
    /// one when there is none yet; a private profile is given its password right after.
    /// </summary>
    public Profile Create(string name)
    {
        string accepted = ProfileValidator.Name(name);
        Profile profile = _profiles.Create(accepted) ?? throw NameTaken();
        try
        {
            _dataFolder.CreateProfileFolder(profile.Id);
            Database.Open(_dataFolder.ProfileDatabasePath(profile.Id), Migrator.Profile).Dispose();
            return profile;
        }
        catch
        {
            _profiles.Remove(profile.Id);
            _dataFolder.RemoveProfileFolder(profile.Id);
            throw;
        }
    }

    /// <summary>
    /// Opens a profile. A profile without a password takes none; one with a password opens only
    /// with it. A change of password that was cut short is finished here.
    /// </summary>
    public ProfileSession Open(long id, string? password = null)
    {
        Profile profile = Find(id);
        if (!profile.HasPassword)
        {
            return new ProfileSession(profile, Database.Open(_dataFolder.ProfileDatabasePath(id), Migrator.Profile), Files(id), key: null);
        }

        byte[] key = Unwrap(id, ProfileValidator.GivenPassword(password));
        try
        {
            return OpenWithKey(profile, key);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            throw;
        }
    }

    /// <summary>
    /// Gets back into a profile whose password was forgotten: the recovery code opens it, a new
    /// password guards it from now on, and a new recovery code takes the place of the one used.
    /// </summary>
    /// <returns>The new recovery code, to be shown once.</returns>
    public string ResetPassword(long id, string recoveryCode, string newPassword)
    {
        Profile profile = Find(id);
        string code = RecoveryCode.Read(recoveryCode ?? "") ?? throw WrongRecoveryCode();
        string accepted = ProfileValidator.NewPassword(newPassword);
        if (!profile.HasPassword)
        {
            throw WrongRecoveryCode();
        }

        byte[] key = KeyWrapper.Unwrap(_profiles.FindRecoveryKey(id)!, code) ?? throw WrongRecoveryCode();
        try
        {
            string newCode = RecoveryCode.Create();
            _profiles.SetKeys(id, KeyWrapper.Wrap(key, accepted), KeyWrapper.Wrap(key, RecoveryCode.Read(newCode)!));
            return newCode;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Whether Windows Hello is set up on this machine, so that it can be offered.</summary>
    public Task<bool> IsHelloAvailableAsync() => WindowsHello.IsAvailableAsync();

    /// <summary>
    /// Lets Windows Hello open an open profile when its password is forgotten, given the password:
    /// Hello makes a pair of keys for it, which asks the person who they are. A wrapping Hello had
    /// before is replaced, and the open profile then says Hello is on.
    /// </summary>
    public async Task TurnOnHelloAsync(ProfileSession session, string password)
    {
        long id = session.Profile.Id;
        byte[] key = Unwrap(id, ProfileValidator.GivenPassword(password));
        try
        {
            string? previous = _profiles.FindHelloKey(id);
            (string credential, byte[] challenge) = HelloKey.Begin();
            byte[] signature = await WindowsHello.CreateAsync(credential, challenge);
            try
            {
                _profiles.SetHelloKey(id, HelloKey.Wrap(key, credential, challenge, signature));
            }
            catch
            {
                WindowsHello.Delete(credential);
                throw;
            }

            if (previous is not null)
            {
                WindowsHello.Delete(HelloKey.Credential(previous));
            }

            session.UseProfile(Find(id));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>
    /// Stops Windows Hello from opening an open profile: its wrapping is forgotten and its pair of
    /// keys deleted from Hello. Nothing happens when Hello was not on.
    /// </summary>
    public void TurnOffHello(ProfileSession session)
    {
        long id = session.Profile.Id;
        string? wrapped = _profiles.FindHelloKey(id);
        if (wrapped is null)
        {
            return;
        }

        _profiles.SetHelloKey(id, null);
        WindowsHello.Delete(HelloKey.Credential(wrapped));
        session.UseProfile(Find(id));
    }

    /// <summary>
    /// Opens a profile with Windows Hello, which asks the person who they are, in place of its
    /// password. When Hello no longer opens it, Hello is turned off for it.
    /// </summary>
    public async Task<ProfileSession> OpenWithHelloAsync(long id)
    {
        Profile profile = Find(id);
        byte[] key = await UnwrapWithHelloAsync(id);
        try
        {
            return OpenWithKey(profile, key);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            throw;
        }
    }

    /// <summary>
    /// Gets back into a profile whose password was forgotten with Windows Hello, which asks the
    /// person who they are: a new password guards it from now on, and the recovery code stays as
    /// it was, since it was not used. When Hello no longer opens it, Hello is turned off for it.
    /// </summary>
    public async Task ResetPasswordWithHelloAsync(long id, string newPassword)
    {
        Find(id);
        string accepted = ProfileValidator.NewPassword(newPassword);
        byte[] key = await UnwrapWithHelloAsync(id);
        try
        {
            _profiles.SetKeys(id, KeyWrapper.Wrap(key, accepted), _profiles.FindRecoveryKey(id));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>
    /// Guards an open profile that has no password with one, and encrypts its database and its
    /// attachment files with a new key: a new private profile, or the public one turned private.
    /// </summary>
    /// <returns>The recovery code, to be shown once.</returns>
    public string SetPassword(ProfileSession session, string password)
    {
        if (session.Profile.HasPassword)
        {
            throw new InvalidOperationException("The profile already has a password: it is changed, not set.");
        }

        string accepted = ProfileValidator.NewPassword(password);
        long id = session.Profile.Id;
        byte[] key = KeyWrapper.CreateKey();
        string code = RecoveryCode.Create();

        // The key is kept first: should anything stop after this, opening the profile finishes the rest.
        _profiles.SetKeys(id, KeyWrapper.Wrap(key, accepted), KeyWrapper.Wrap(key, RecoveryCode.Read(code)!));
        session.Database.ChangeKey(key);
        session.UseKey(key, Find(id));
        session.SealFiles();
        return code;
    }

    /// <summary>
    /// Guards an open profile with another password. The data is not encrypted again, and the
    /// recovery code stays the same.
    /// </summary>
    public void ChangePassword(ProfileSession session, string currentPassword, string newPassword)
    {
        long id = session.Profile.Id;
        byte[] key = Unwrap(id, ProfileValidator.GivenPassword(currentPassword));
        try
        {
            string accepted = ProfileValidator.NewPassword(newPassword);
            _profiles.SetKeys(id, KeyWrapper.Wrap(key, accepted), _profiles.FindRecoveryKey(id));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Gives an open profile another name, which the open profile then carries.</summary>
    public Profile Rename(ProfileSession session, string name)
    {
        string accepted = ProfileValidator.Name(name);
        long id = session.Profile.Id;
        Find(id);
        Profile renamed = _profiles.Rename(id, accepted) ?? throw NameTaken();
        session.UseProfile(renamed);
        return renamed;
    }

    /// <summary>
    /// Deletes an open profile with everything it has, files included, given its password when it
    /// has one. It is closed first, and it cannot be undone.
    /// </summary>
    public void Delete(ProfileSession session, string? password = null)
    {
        long id = session.Profile.Id;
        if (session.Profile.HasPassword)
        {
            CryptographicOperations.ZeroMemory(Unwrap(id, ProfileValidator.GivenPassword(password)));
        }

        string? helloKey = _profiles.FindHelloKey(id);
        session.Dispose();

        // The row goes first: a profile with no row is not listed, and a folder or a pair of keys left behind is housekeeping.
        _profiles.Remove(id);
        _dataFolder.RemoveProfileFolder(id);
        if (helloKey is not null)
        {
            WindowsHello.Delete(HelloKey.Credential(helloKey));
        }
    }

    // -- Helpers -- //

    private ProfileSession OpenWithKey(Profile profile, byte[] key)
    {
        string path = _dataFolder.ProfileDatabasePath(profile.Id);
        Database? database = Database.OpenIfKeyFits(path, Migrator.Profile, key);
        bool interrupted = database is null;
        if (database is null)
        {
            // The key was kept but the database was not encrypted with it: setting the password
            // was cut short. It is finished now. A file neither way opens is a failure to report.
            database = Database.OpenIfKeyFits(path, Migrator.Profile) ?? Database.Open(path, Migrator.Profile, key);
            database.ChangeKey(key);
        }

        var session = new ProfileSession(profile, database, Files(profile.Id), key);
        if (interrupted)
        {
            session.SealFiles();
        }

        return session;
    }

    private byte[] Unwrap(long id, string password)
    {
        string wrapped = _profiles.FindPasswordKey(id) ?? throw new WrongPasswordException(ErrorCodes.WrongPassword, "The profile has no password.");
        return KeyWrapper.Unwrap(wrapped, password) ?? throw new WrongPasswordException(ErrorCodes.WrongPassword, "The password does not open the profile.");
    }

    private Profile Find(long id) => _profiles.Find(id) ?? throw new NotFoundException("Profile not found");

    private FileStore Files(long id) => new(_dataFolder.UploadsFolder(id));

    private static ConflictException NameTaken() => new(ErrorCodes.ProfileExists, "A profile with that name already exists");

    /// <summary>
    /// The key of a profile, unwrapped with the signature Windows Hello makes once the person says
    /// who they are. A wrapping Hello no longer opens is forgotten, with its pair of keys.
    /// </summary>
    private async Task<byte[]> UnwrapWithHelloAsync(long id)
    {
        string wrapped = _profiles.FindHelloKey(id) ?? throw HelloKeyLost();
        byte[]? signature = await WindowsHello.SignAsync(HelloKey.Credential(wrapped), HelloKey.Challenge(wrapped));
        byte[]? key = signature is null ? null : HelloKey.Unwrap(wrapped, signature);
        if (key is null)
        {
            // Another machine, a Windows installed again, or a PIN removed: the wrapping opens nothing any more.
            _profiles.SetHelloKey(id, null);
            WindowsHello.Delete(HelloKey.Credential(wrapped));
            throw HelloKeyLost();
        }

        return key;
    }

    private static HelloException HelloKeyLost() => new(ErrorCodes.HelloKeyLost, "Windows Hello no longer opens the profile.");

    private static WrongPasswordException WrongRecoveryCode() => new(ErrorCodes.WrongRecoveryCode, "The recovery code does not open the profile.");
}
