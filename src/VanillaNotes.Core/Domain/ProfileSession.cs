using System.Security.Cryptography;
using VanillaNotes.Core.Data;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Security;
using VanillaNotes.Core.Storage;

namespace VanillaNotes.Core.Domain;

/// <summary>
/// A profile that is open: its database, its attachment files, and, when it has a password, the
/// key of its data, which lives here in memory only and nowhere else. Everything the person does
/// with their notes happens through one of these; closing it forgets the key.
/// </summary>
public sealed class ProfileSession : IDisposable
{
    private byte[]? _key;

    internal ProfileSession(Profile profile, Database database, FileStore files, byte[]? key)
    {
        Profile = profile;
        Database = database;
        Files = files;
        _key = key;
    }

    /// <summary>The profile that is open, as it was when it was opened or when its password last changed.</summary>
    public Profile Profile { get; private set; }

    /// <summary>The database of the profile.</summary>
    public Database Database { get; }

    /// <summary>The attachment files of the profile, as they are on disk.</summary>
    public FileStore Files { get; }

    /// <summary>
    /// Stores the bytes of a new attachment file, sealed with the key of the profile when it has one.
    /// </summary>
    /// <returns>The name the file is stored under.</returns>
    internal string SaveFile(ReadOnlySpan<byte> bytes) => Files.Save(_key is null ? bytes : FileCipher.Seal(_key, bytes));

    /// <summary>
    /// The bytes of an attachment file as they were given. A file the key does not open is taken
    /// as it is: it is one left plain by a change of password that was cut short.
    /// </summary>
    internal byte[] ReadFile(string name)
    {
        byte[] stored = Files.Read(name);
        return _key is null ? stored : FileCipher.OpenOrNothing(_key, stored) ?? stored;
    }

    /// <summary>
    /// Seals with the key of the profile every attachment file that is not sealed with it yet.
    /// </summary>
    internal void SealFiles()
    {
        if (_key is null)
        {
            return;
        }

        foreach (string name in Files.List())
        {
            byte[] stored = Files.Read(name);
            if (FileCipher.OpenOrNothing(_key, stored) is null)
            {
                Files.Replace(name, FileCipher.Seal(_key, stored));
            }
        }
    }

    /// <summary>
    /// Takes the key the profile has from now on, once a password was set on it.
    /// </summary>
    internal void UseKey(byte[] key, Profile profile)
    {
        if (_key is not null && !ReferenceEquals(_key, key))
        {
            CryptographicOperations.ZeroMemory(_key);
        }

        _key = key;
        Profile = profile;
    }

    /// <summary>Takes the profile as it is now, after it was renamed.</summary>
    internal void UseProfile(Profile profile) => Profile = profile;

    /// <summary>
    /// Closes the profile: its database is let go, and its key is wiped from memory.
    /// </summary>
    public void Dispose()
    {
        Database.Dispose();
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
    }
}
