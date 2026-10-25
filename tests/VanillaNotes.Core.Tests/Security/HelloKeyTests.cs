using System.Security.Cryptography;
using Xunit;
using VanillaNotes.Core.Security;

namespace VanillaNotes.Core.Tests.Security;

/// <summary>The key of a profile locked by a signature of Windows Hello, and opened only by that signature.</summary>
public sealed class HelloKeyTests
{
    /// <summary>A signature as Hello gives one: RSA, 256 bytes.</summary>
    private static byte[] Signature() => RandomNumberGenerator.GetBytes(256);

    [Fact]
    public void TheSignatureThatWrappedAKeyUnwrapsIt()
    {
        byte[] key = KeyWrapper.CreateKey();
        byte[] signature = Signature();
        (string credential, byte[] challenge) = HelloKey.Begin();

        string wrapped = HelloKey.Wrap(key, credential, challenge, signature);

        Assert.Equal(key, HelloKey.Unwrap(wrapped, signature));
    }

    [Fact]
    public void AnotherSignatureUnwrapsNothing()
    {
        (string credential, byte[] challenge) = HelloKey.Begin();
        string wrapped = HelloKey.Wrap(KeyWrapper.CreateKey(), credential, challenge, Signature());

        Assert.Null(HelloKey.Unwrap(wrapped, Signature()));
    }

    [Fact]
    public void TheWrappedFormSaysWhichPairOfKeysSignsAndWhat()
    {
        (string credential, byte[] challenge) = HelloKey.Begin();

        string wrapped = HelloKey.Wrap(KeyWrapper.CreateKey(), credential, challenge, Signature());

        Assert.StartsWith("hello-rsa$", wrapped, StringComparison.Ordinal);
        Assert.Equal(credential, HelloKey.Credential(wrapped));
        Assert.Equal(challenge, HelloKey.Challenge(wrapped));
    }

    [Fact]
    public void EveryWrappingTakesANewPairOfKeysAndANewValue()
    {
        (string firstCredential, byte[] firstChallenge) = HelloKey.Begin();
        (string secondCredential, byte[] secondChallenge) = HelloKey.Begin();

        Assert.StartsWith("Vanilla Notes ", firstCredential, StringComparison.Ordinal);
        Assert.NotEqual(firstCredential, secondCredential);
        Assert.NotEqual(firstChallenge, secondChallenge);
    }

    [Fact]
    public void TheWrappedFormDoesNotHoldTheKeyNorTheSignature()
    {
        byte[] key = KeyWrapper.CreateKey();
        byte[] signature = Signature();
        (string credential, byte[] challenge) = HelloKey.Begin();

        string wrapped = HelloKey.Wrap(key, credential, challenge, signature);

        Assert.DoesNotContain(Convert.ToHexString(key), wrapped, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexString(signature), wrapped, StringComparison.Ordinal);
    }

    [Fact]
    public void ATextNotWrittenByTheApplicationIsRefused()
    {
        Assert.Throws<FormatException>(() => HelloKey.Unwrap(KeyWrapper.Wrap(KeyWrapper.CreateKey(), "a password"), Signature()));
    }
}
