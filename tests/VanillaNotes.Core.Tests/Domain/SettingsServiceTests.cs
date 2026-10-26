using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>The settings a profile keeps, and their defaults.</summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly TestApplication _app = new();

    private readonly TestApplication.OpenProfile _ana;

    public SettingsServiceTests()
    {
        _ana = _app.CreateProfile();
    }

    public void Dispose() => _app.Dispose();

    [Fact]
    public void AProfileThatNeverChangedASettingHasTheDefaults()
    {
        Assert.Equal(new ProfileSettings("en", null, "normal", "visual", true, "relative", "15", "updated"), _ana.Settings.Get());
    }

    [Fact]
    public void SettingsAreKeptByTheProfile()
    {
        var settings = new ProfileSettings("es", "dark", "large", "markdown", false, "exact", "never", "title");

        _ana.Settings.Update(settings);
        _ana.Session.Dispose();

        Assert.Equal(settings, _app.Open(_ana.Id).Settings.Get());
    }

    [Theory]
    [InlineData("fr", "light", "normal", "visual", "relative", "language")]
    [InlineData("en", "blue", "normal", "visual", "relative", "theme")]
    [InlineData("en", "light", "huge", "visual", "relative", "textSize")]
    [InlineData("en", "light", "normal", "both", "relative", "editorMode")]
    [InlineData("en", "light", "normal", "visual", "soon", "dateStyle")]
    public void AValueASettingDoesNotAllowIsRefused(string language, string theme, string textSize, string editorMode, string dateStyle, string field)
    {
        var settings = new ProfileSettings(language, theme, textSize, editorMode, true, dateStyle);

        Assert.Equal(field, Assert.Throws<ValidationException>(() => _ana.Settings.Update(settings)).Field);
        Assert.Equal(ProfileSettings.Default, _ana.Settings.Get());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("10")]
    [InlineData("Never")]
    public void ATimeToCloseTheProfileItDoesNotAllowIsRefused(string lockAfter)
    {
        Assert.Equal("lockAfter", Assert.Throws<ValidationException>(() => _ana.Settings.Update(ProfileSettings.Default with { LockAfter = lockAfter })).Field);
    }

    [Fact]
    public void AnOrderOfTheListItDoesNotHaveIsRefused()
    {
        Assert.Equal("noteOrder", Assert.Throws<ValidationException>(() => _ana.Settings.Update(ProfileSettings.Default with { NoteOrder = "random" })).Field);
    }

    [Fact]
    public void TheSettingsOfAProfileWithAPasswordAreEncryptedWithIt()
    {
        TestApplication.OpenProfile zoe = _app.CreateProfile("Zoe", TestApplication.Password);

        zoe.Settings.Update(ProfileSettings.Default with { Theme = "neutral" });
        zoe.Session.Dispose();

        Assert.False(TestApplication.DiskHolds(_app.DataFolder.ProfileDatabasePath(zoe.Id), "neutral"));
    }
}
