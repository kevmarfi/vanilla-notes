using System.Globalization;
using System.Security.Cryptography;

namespace VanillaNotes.Core.Security;

/// <summary>
/// The key of the data of a profile, and the two ways it is kept: wrapped by the password and
/// wrapped by the recovery code. The key itself is random; a secret only locks it away. A wrong
/// secret is told apart by the encryption itself, so no password is ever stored or compared.
/// </summary>
public static class KeyWrapper
{
    /// <summary>How long the key of a profile is, in bytes.</summary>
    public const int KeyLength = 32;

    /// <summary>How many times a secret is stretched before it locks a key; written into every wrapped key, so it can be raised later.</summary>
    public const int Iterations = 600_000;

    /// <summary>The name the wrapped form starts with, for the way a secret is stretched.</summary>
    private const string _scheme = "pbkdf2-sha256";

    /// <summary>How long the random salt of a wrapping is, in bytes.</summary>
    private const int _saltLength = 16;

    /// <summary>
    /// Makes a new random key for the data of a profile.
    /// </summary>
    public static byte[] CreateKey() => RandomNumberGenerator.GetBytes(KeyLength);

    /// <summary>
    /// Locks the key of a profile with a secret, as the text the catalog keeps. Wrapping the same
    /// key twice gives two different texts, since each takes a new salt.
    /// </summary>
    public static string Wrap(ReadOnlySpan<byte> key, string secret)
    {
        if (key.Length != KeyLength)
        {
            throw new ArgumentException($"A key is {KeyLength} bytes long.", nameof(key));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(_saltLength);
        byte[] wrappingKey = Stretch(secret, salt, Iterations);
        try
        {
            // Sealed is the nonce, then the tag, then the key encrypted: written as three parts.
            byte[] sealedKey = FileCipher.Seal(wrappingKey, key);
            return string.Join(
                '$',
                _scheme,
                Iterations.ToString(CultureInfo.InvariantCulture),
                Convert.ToHexString(salt),
                Convert.ToHexString(sealedKey, 0, FileCipher.NonceLength),
                Convert.ToHexString(sealedKey, FileCipher.NonceLength, FileCipher.TagLength),
                Convert.ToHexString(sealedKey, FileCipher.Overhead, KeyLength));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrappingKey);
        }
    }

    /// <summary>
    /// Opens a wrapped key with a secret.
    /// </summary>
    /// <returns>The key of the profile, or nothing when the secret is not the one that locked it.</returns>
    public static byte[]? Unwrap(string wrapped, string secret)
    {
        string[] parts = wrapped.Split('$');
        if (parts.Length != 6 || parts[0] != _scheme || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations))
        {
            throw new FormatException("The wrapped key is not in the form this application writes.");
        }

        byte[] wrappingKey = Stretch(secret, Convert.FromHexString(parts[2]), iterations);
        try
        {
            return FileCipher.Open(wrappingKey, Convert.FromHexString(parts[3] + parts[4] + parts[5]));
        }
        catch (AuthenticationTagMismatchException)
        {
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrappingKey);
        }
    }

    private static byte[] Stretch(string secret, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA256, KeyLength);
}
