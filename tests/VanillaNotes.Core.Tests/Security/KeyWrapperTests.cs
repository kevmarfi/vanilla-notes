using System.Globalization;
using Xunit;
using VanillaNotes.Core.Security;

namespace VanillaNotes.Core.Tests.Security;

/// <summary>The key of a profile locked by a secret, and opened only by that secret.</summary>
public sealed class KeyWrapperTests
{
    [Fact]
    public void TheSecretThatWrappedAKeyUnwrapsIt()
    {
        byte[] key = KeyWrapper.CreateKey();

        string wrapped = KeyWrapper.Wrap(key, "a password");

        Assert.Equal(key, KeyWrapper.Unwrap(wrapped, "a password"));
    }

    [Fact]
    public void AnotherSecretUnwrapsNothing()
    {
        string wrapped = KeyWrapper.Wrap(KeyWrapper.CreateKey(), "a password");

        Assert.Null(KeyWrapper.Unwrap(wrapped, "A password"));
    }

    [Fact]
    public void WrappingTheSameKeyTwiceGivesTwoDifferentTexts()
    {
        byte[] key = KeyWrapper.CreateKey();

        Assert.NotEqual(KeyWrapper.Wrap(key, "a password"), KeyWrapper.Wrap(key, "a password"));
    }

    [Fact]
    public void TheWrappedFormCarriesTheWayAndTheCostOfTheStretching()
    {
        string[] parts = KeyWrapper.Wrap(KeyWrapper.CreateKey(), "a password").Split('$');

        Assert.Equal(6, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.Equal(KeyWrapper.Iterations.ToString(CultureInfo.InvariantCulture), parts[1]);
    }

    [Fact]
    public void TheWrappedFormDoesNotHoldTheKey()
    {
        byte[] key = KeyWrapper.CreateKey();

        string wrapped = KeyWrapper.Wrap(key, "a password");

        Assert.DoesNotContain(Convert.ToHexString(key), wrapped, StringComparison.Ordinal);
    }

    [Fact]
    public void ATextNotWrittenByTheApplicationIsRefused()
    {
        Assert.Throws<FormatException>(() => KeyWrapper.Unwrap("not a wrapped key", "a password"));
    }

    [Fact]
    public void OnlyAKeyOfTheRightLengthIsWrapped()
    {
        Assert.Throws<ArgumentException>(() => KeyWrapper.Wrap(new byte[16], "a password"));
    }
}
