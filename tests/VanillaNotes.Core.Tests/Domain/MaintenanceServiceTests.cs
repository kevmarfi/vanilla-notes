using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>Housekeeping of what a write cut short leaves behind.</summary>
public sealed class MaintenanceServiceTests : IDisposable
{
    private readonly TestApplication _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public void TheFolderOfAProfileTheCatalogNoLongerListsIsRemoved()
    {
        long kept = _app.ProfileService.Create("Ana").Id;
        long gone = _app.ProfileService.Create("Zoe").Id;
        _app.Profiles.Remove(gone);

        Assert.Equal(1, _app.Maintenance.RemoveLeftoverProfileFolders());
        Assert.True(Directory.Exists(_app.DataFolder.ProfileFolder(kept)));
        Assert.False(Directory.Exists(_app.DataFolder.ProfileFolder(gone)));
    }

    [Fact]
    public void TheDiskIsLowOnlyBelowWhatMustStayFree()
    {
        using var full = new TestApplication(diskIsFull: true);

        Assert.False(_app.Maintenance.IsDiskLow());
        Assert.True(full.Maintenance.IsDiskLow());
    }
}
