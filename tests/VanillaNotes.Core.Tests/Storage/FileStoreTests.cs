using VanillaNotes.Core.Storage;
using Xunit;

namespace VanillaNotes.Core.Tests.Storage;

/// <summary>Files kept under random names, and never outside their folder.</summary>
public sealed class FileStoreTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("vanilla-notes-tests-");

    private readonly FileStore _store;

    public FileStoreTests()
    {
        _store = new FileStore(_folder.FullName);
    }

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public void AFileIsStoredUnderANewRandomNameAndReadBack()
    {
        string first = _store.Save([1, 2, 3]);
        string second = _store.Save([1, 2, 3]);

        Assert.Matches("^[0-9a-f]{32}$", first);
        Assert.NotEqual(first, second);
        Assert.Equal([1, 2, 3], _store.Read(first));
    }

    [Fact]
    public void AFileIsReplacedWholeAndLeavesNothingBehind()
    {
        string name = _store.Save([1, 2, 3]);

        _store.Replace(name, [4, 5]);

        Assert.Equal([4, 5], _store.Read(name));
        Assert.Equal([name], _store.List());
        Assert.Single(_folder.GetFiles());
    }

    [Fact]
    public void RemovingAFileThatIsNotThereIsNotAFailure()
    {
        string name = _store.Save([1]);

        _store.Remove(name);
        _store.Remove(name);

        Assert.Empty(_store.List());
    }

    [Fact]
    public void TheListHoldsOnlyTheFilesTheStoreMade()
    {
        string name = _store.Save([1]);
        File.WriteAllText(Path.Combine(_folder.FullName, "notes.txt"), "not a stored file");

        Assert.Equal([name], _store.List());
    }

    [Theory]
    [InlineData("..\\notes.db")]
    [InlineData("../notes.db")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("")]
    public void ANameTheStoreDidNotMakeIsRefused(string name)
    {
        Assert.Throws<ArgumentException>(() => _store.Read(name));
        Assert.Throws<ArgumentException>(() => _store.Remove(name));
    }
}
