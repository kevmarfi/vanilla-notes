using System.Security.Cryptography;
using System.Text;

namespace VanillaNotes.Core.Security;

/// <summary>
/// The key of a profile wrapped by Windows Hello, as the catalog keeps it. Hello signs a random
/// value of the profile, and the signature, the same every time, makes the key that wraps it; the
/// text kept says which pair of keys of Hello signs, and what it signs. Asking Hello for the
/// signature is the business of the Windows Hello class; this one only wraps and unwraps.
/// </summary>
public static class HelloKey
{
    /// <summary>The name the wrapped form starts with, for the kind of signature it is made from.</summary>
    private const string _scheme = "hello-rsa";

    /// <summary>What the name of a pair of keys of Hello starts with, before the number of its wrapping.</summary>
    private const string _credentialPrefix = "Vanilla Notes ";

    /// <summary>The information the signature is turned into a key with, so that it makes no other key.</summary>
    private static readonly byte[] _info = Encoding.UTF8.GetBytes("vanilla-notes/hello-key");

    /// <summary>How long the random number that names a pair of keys of Hello is, in bytes.</summary>
    private const int _credentialLength = 16;

    /// <summary>How long the random value Hello signs is, in bytes.</summary>
    private const int _challengeLength = 32;

    /// <summary>How long the random salt of a wrapping is, in bytes.</summary>
    private const int _saltLength = 16;

    /// <summary>
    /// What a new wrapping needs from Hello before it can be made: the name of a new pair of keys,
    /// and a new value for it to sign.
    /// </summary>
    public static (string Credential, byte[] Challenge) Begin() =>
        (_credentialPrefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(_credentialLength)), RandomNumberGenerator.GetBytes(_challengeLength));

    /// <summary>
    /// Locks the key of a profile with the signature Hello gave for the value of a new wrapping,
    /// as the text the catalog keeps. Wrapping the same key twice gives two different texts.
    /// </summary>
    public static string Wrap(ReadOnlySpan<byte> key, string credential, byte[] challenge, byte[] signature)
    {
        if (key.Length != KeyWrapper.KeyLength)
        {
            throw new ArgumentException($"A key is {KeyWrapper.KeyLength} bytes long.", nameof(key));
        }

        if (!credential.StartsWith(_credentialPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("The pair of keys was not named by this class.", nameof(credential));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(_saltLength);
        byte[] wrappingKey = Derive(signature, salt);
        try
        {
            // Sealed is the nonce, then the tag, then the key encrypted: written as three parts.
            byte[] sealedKey = FileCipher.Seal(wrappingKey, key);
            return string.Join(
                '$',
                _scheme,
                credential[_credentialPrefix.Length..],
                Convert.ToHexString(challenge),
                Convert.ToHexString(salt),
                Convert.ToHexString(sealedKey, 0, FileCipher.NonceLength),
                Convert.ToHexString(sealedKey, FileCipher.NonceLength, FileCipher.TagLength),
                Convert.ToHexString(sealedKey, FileCipher.Overhead, KeyWrapper.KeyLength));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrappingKey);
        }
    }

    /// <summary>The name of the pair of keys of Hello that signs for a wrapped key.</summary>
    public static string Credential(string wrapped) => _credentialPrefix + Parts(wrapped)[1];

    /// <summary>The value Hello is to sign to open a wrapped key.</summary>
    public static byte[] Challenge(string wrapped) => Convert.FromHexString(Parts(wrapped)[2]);

    /// <summary>
    /// Opens a wrapped key with the signature Hello gave for its value.
    /// </summary>
    /// <returns>The key of the profile, or nothing when the signature is not the one that locked it.</returns>
    public static byte[]? Unwrap(string wrapped, byte[] signature)
    {
        string[] parts = Parts(wrapped);
        byte[] wrappingKey = Derive(signature, Convert.FromHexString(parts[3]));
        try
        {
            return FileCipher.Open(wrappingKey, Convert.FromHexString(parts[4] + parts[5] + parts[6]));
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

    private static string[] Parts(string wrapped)
    {
        string[] parts = wrapped.Split('$');
        return parts.Length == 7 && parts[0] == _scheme
            ? parts
            : throw new FormatException("The key wrapped by Windows Hello is not in the form this application writes.");
    }

    private static byte[] Derive(byte[] signature, byte[] salt) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, signature, KeyWrapper.KeyLength, salt, _info);
}
