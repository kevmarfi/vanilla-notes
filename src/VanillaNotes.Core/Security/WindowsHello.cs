using System.Runtime.InteropServices;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using VanillaNotes.Core.Errors;

namespace VanillaNotes.Core.Security;

/// <summary>
/// Windows Hello, as the application asks it things: whether it is set up, to make a pair of keys
/// for a profile, to sign with it, and to delete it. Every signature asks the person for the PIN,
/// the fingerprint, or the face of the Windows account, in a window of Windows. It is the one
/// place of the application tied to Windows outside the interface, and is not covered by the
/// tests, which run with no person to answer it.
/// </summary>
public static class WindowsHello
{
    /// <summary>Whether Windows Hello is set up for the Windows account, so that it can be offered.</summary>
    public static async Task<bool> IsAvailableAsync() => await KeyCredentialManager.IsSupportedAsync();

    /// <summary>
    /// Makes a new pair of keys in Hello, in place of any other of the same name, and signs a
    /// value with it.
    /// </summary>
    /// <returns>The signature of the value.</returns>
    public static async Task<byte[]> CreateAsync(string credential, byte[] challenge)
    {
        KeyCredentialRetrievalResult created = await KeyCredentialManager.RequestCreateAsync(credential, KeyCredentialCreationOption.ReplaceExisting);
        Check(created.Status);
        return await SignAsync(created.Credential, challenge)
            ?? throw new HelloException(ErrorCodes.HelloUnavailable, "The pair of keys just made could not sign.");
    }

    /// <summary>
    /// Signs a value with a pair of keys Hello already has.
    /// </summary>
    /// <returns>The signature of the value, or nothing when Hello no longer has that pair.</returns>
    public static async Task<byte[]?> SignAsync(string credential, byte[] challenge)
    {
        KeyCredentialRetrievalResult opened = await KeyCredentialManager.OpenAsync(credential);
        if (opened.Status == KeyCredentialStatus.NotFound)
        {
            return null;
        }

        Check(opened.Status);
        return await SignAsync(opened.Credential, challenge);
    }

    /// <summary>
    /// Deletes a pair of keys from Hello. A pair left behind holds nothing, since what it signs no
    /// longer opens any key on disk, so a deletion that fails is let go.
    /// </summary>
    public static void Delete(string credential)
    {
        try
        {
            KeyCredentialManager.DeleteAsync(credential).AsTask().GetAwaiter().GetResult();
        }
        catch (COMException)
        {
            // Hello no longer had the pair, or would not delete it: there is nothing left to clean up.
        }
    }

    private static async Task<byte[]?> SignAsync(KeyCredential credential, byte[] challenge)
    {
        KeyCredentialOperationResult signed = await credential.RequestSignAsync(CryptographicBuffer.CreateFromByteArray(challenge));
        if (signed.Status == KeyCredentialStatus.NotFound)
        {
            return null;
        }

        Check(signed.Status);
        IBuffer signature = signed.Result;
        CryptographicBuffer.CopyToByteArray(signature, out byte[] bytes);
        return bytes;
    }

    /// <summary>Turns what Hello answered into a failure of the application, when it is not a success.</summary>
    private static void Check(KeyCredentialStatus status)
    {
        switch (status)
        {
            case KeyCredentialStatus.Success:
                return;
            case KeyCredentialStatus.UserCanceled:
            case KeyCredentialStatus.UserPrefersPassword:
                throw new HelloException(ErrorCodes.HelloCanceled, "The person closed the window of Windows Hello.");
            default:
                throw new HelloException(ErrorCodes.HelloUnavailable, $"Windows Hello answered {status}.");
        }
    }
}
