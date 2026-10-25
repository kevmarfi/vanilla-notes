using VanillaNotes.Core.Models;
using VanillaNotes.Core.Storage;
using Xunit;

namespace VanillaNotes.Core.Tests.Storage;

/// <summary>Where the window was when it last closed, and a file that says nothing usable.</summary>
public sealed class WindowPlacementFileTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("vanilla-notes-tests-");

    private readonly WindowPlacementFile _file;

    public WindowPlacementFileTests()
    {
        _file = new WindowPlacementFile(_folder.FullName);
    }

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public void AWindowThatNeverClosedHasNoPlacement()
    {
        Assert.Null(_file.Find());
    }

    [Fact]
    public void ThePlacementIsKeptAndReadBack()
    {
        var placement = new WindowPlacement(-1200.5, 40, 1280, 800, true);

        _file.Save(placement);

        Assert.Equal(placement, new WindowPlacementFile(_folder.FullName).Find());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"left\":0,\"top\":0,\"width\":0,\"height\":600,\"isMaximized\":false}")]
    [InlineData("{\"left\":0,\"top\":0,\"width\":800,\"height\":-1,\"isMaximized\":false}")]
    public void AFileThatSaysNothingAWindowCanTakeIsNoPlacement(string text)
    {
        File.WriteAllText(Path.Combine(_folder.FullName, "window.json"), text);

        Assert.Null(_file.Find());
    }

    [Fact]
    public void APlacementThatCannotBeWrittenIsForgottenWithoutAFailure()
    {
        // A file where the folder should be: nothing can be written under it.
        string blocked = Path.Combine(_folder.FullName, "blocked");
        File.WriteAllText(blocked, "");
        var file = new WindowPlacementFile(blocked);

        file.Save(new WindowPlacement(0, 0, 800, 600, false));

        Assert.Null(file.Find());
    }
}
