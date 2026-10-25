using System.Security.Cryptography;

namespace VanillaNotes.Core.Security;

/// <summary>
/// Seals bytes with a key so that only that key opens them, and tells when they were changed
/// since. It is what keeps the attachments of a profile with a password unreadable on disk, and
/// what locks the key of a profile inside its wrapped form.
/// </summary>
public static class FileCipher
{
    /// <summary>How long the random value that makes every sealing different is, in bytes; it comes first.</summary>
    public const int NonceLength = 12;

    /// <summary>How long the check that tells a changed or wrongly opened sealing is, in bytes; it comes after the nonce.</summary>
    public const int TagLength = 16;

    /// <summary>How many bytes sealing adds to what is sealed, before the bytes themselves.</summary>
    public const int Overhead = NonceLength + TagLength;

    /// <summary>
    /// Seals bytes with a key. Sealing the same bytes twice gives two different results.
    /// </summary>
    public static byte[] Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plain)
    {
        byte[] sealedBytes = new byte[Overhead + plain.Length];
        Span<byte> nonce = sealedBytes.AsSpan(0, NonceLength);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagLength);
        aes.Encrypt(nonce, plain, sealedBytes.AsSpan(Overhead), sealedBytes.AsSpan(NonceLength, TagLength));
        return sealedBytes;
    }

    /// <summary>
    /// Opens what was sealed with a key. Another key, or a single byte changed since the sealing,
    /// fails with the exception of the platform for a check that does not match.
    /// </summary>
    public static byte[] Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> sealedBytes)
    {
        if (sealedBytes.Length < Overhead)
        {
            throw new AuthenticationTagMismatchException("The bytes are too short to have been sealed.");
        }

        byte[] plain = new byte[sealedBytes.Length - Overhead];
        using var aes = new AesGcm(key, TagLength);
        aes.Decrypt(sealedBytes[..NonceLength], sealedBytes[Overhead..], sealedBytes.Slice(NonceLength, TagLength), plain);
        return plain;
    }

    /// <summary>
    /// Opens what was sealed with a key, or tells that the bytes were not sealed with it: another
    /// key sealed them, they changed since, or they were never sealed at all.
    /// </summary>
    /// <returns>What was sealed, or nothing when the key does not open the bytes.</returns>
    public static byte[]? OpenOrNothing(ReadOnlySpan<byte> key, ReadOnlySpan<byte> bytes)
    {
        try
        {
            return Open(key, bytes);
        }
        catch (AuthenticationTagMismatchException)
        {
            return null;
        }
    }
}
