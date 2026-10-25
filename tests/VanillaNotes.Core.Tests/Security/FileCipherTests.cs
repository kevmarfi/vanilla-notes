using System.Security.Cryptography;
using System.Text;
using Xunit;
using VanillaNotes.Core.Security;

namespace VanillaNotes.Core.Tests.Security;

/// <summary>Bytes sealed with a key, opened only by that key, and only as they were sealed.</summary>
public sealed class FileCipherTests
{
    /// <summary>What is sealed in these tests: text, so that it can be looked for in what is sealed.</summary>
    private static readonly byte[] _plain = Encoding.UTF8.GetBytes("The contents of an attachment.");

    [Fact]
    public void TheKeyThatSealedTheBytesOpensThem()
    {
        byte[] key = KeyWrapper.CreateKey();

        byte[] sealedBytes = FileCipher.Seal(key, _plain);

        Assert.Equal(_plain, FileCipher.Open(key, sealedBytes));
    }

    [Fact]
    public void SealedBytesDoNotHoldWhatWasSealed()
    {
        byte[] sealedBytes = FileCipher.Seal(KeyWrapper.CreateKey(), _plain);

        Assert.Equal(_plain.Length + FileCipher.Overhead, sealedBytes.Length);
        Assert.Equal(-1, sealedBytes.AsSpan().IndexOf(_plain.AsSpan(0, 8)));
    }

    [Fact]
    public void SealingTheSameBytesTwiceGivesTwoDifferentResults()
    {
        byte[] key = KeyWrapper.CreateKey();

        Assert.NotEqual(FileCipher.Seal(key, _plain), FileCipher.Seal(key, _plain));
    }

    [Fact]
    public void AnotherKeyCannotOpenThem()
    {
        byte[] sealedBytes = FileCipher.Seal(KeyWrapper.CreateKey(), _plain);

        Assert.Throws<AuthenticationTagMismatchException>(() => FileCipher.Open(KeyWrapper.CreateKey(), sealedBytes));
    }

    [Fact]
    public void OneByteChangedIsFound()
    {
        byte[] key = KeyWrapper.CreateKey();
        byte[] sealedBytes = FileCipher.Seal(key, _plain);
        sealedBytes[^1] ^= 1;

        Assert.Throws<AuthenticationTagMismatchException>(() => FileCipher.Open(key, sealedBytes));
    }

    [Fact]
    public void BytesTooShortToHaveBeenSealedAreRefused()
    {
        Assert.Throws<AuthenticationTagMismatchException>(() => FileCipher.Open(KeyWrapper.CreateKey(), new byte[FileCipher.Overhead - 1]));
    }

    [Fact]
    public void NothingSealedOpensAsNothing()
    {
        byte[] key = KeyWrapper.CreateKey();

        Assert.Empty(FileCipher.Open(key, FileCipher.Seal(key, [])));
    }
}
