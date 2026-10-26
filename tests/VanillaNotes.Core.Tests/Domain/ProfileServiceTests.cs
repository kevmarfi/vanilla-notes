using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Security;
using VanillaNotes.Core.Tests.Helpers;
using Xunit;

namespace VanillaNotes.Core.Tests.Domain;

/// <summary>Making, opening, guarding, getting back into, renaming, and deleting a profile.</summary>
public sealed class ProfileServiceTests : IDisposable
{
    private readonly TestApplication _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public void AProfileIsMadeWithNoPasswordAndListedByName()
    {
        _app.ProfileService.Create("  Zoe ");
        _app.ProfileService.Create("Ana");

        IReadOnlyList<Profile> profiles = _app.ProfileService.List();

        Assert.Equal(["Ana", "Zoe"], profiles.Select(profile => profile.Name));
        Assert.All(profiles, profile => Assert.False(profile.HasPassword));
    }

    [Fact]
    public void ThePublicProfileIsTheFirstMadeWithoutAPassword()
    {
        _app.CreateProfile("Zoe", TestApplication.Password);
        long first = _app.ProfileService.Create("Public").Id;
        _app.ProfileService.Create("Ana");

        Assert.Equal(first, _app.ProfileService.FindPublic()?.Id);
    }

    [Fact]
    public void ThereIsNoPublicProfileWhenEveryProfileHasAPassword()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile("Ana");
        _app.ProfileService.SetPassword(ana.Session, TestApplication.Password);

        Assert.Null(_app.ProfileService.FindPublic());
    }

    [Fact]
    public void TwoProfilesCannotHaveTheSameNameRegardlessOfCase()
    {
        _app.ProfileService.Create("Ana");

        var failure = Assert.Throws<ConflictException>(() => _app.ProfileService.Create("ANA"));

        Assert.Equal(ErrorCodes.ProfileExists, failure.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A name that goes on for more than fifty characters in all")]
    public void ANameMustBeOneToFiftyCharacters(string name)
    {
        var failure = Assert.Throws<ValidationException>(() => _app.ProfileService.Create(name));

        Assert.Equal("name", failure.Field);
    }

    [Fact]
    public void AProfileWithoutAPasswordOpensWithNone()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile();
        ana.Notes.Create("A note");
        ana.Session.Dispose();

        TestApplication.OpenProfile again = _app.Open(ana.Id);

        Assert.Equal(1, again.Notes.List(new NoteQuery()).Total);
    }

    [Fact]
    public void SettingAPasswordEncryptsWhatTheProfileAlreadyHas()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile();
        ana.Notes.Create("Groceries for the weekend");

        string code = _app.ProfileService.SetPassword(ana.Session, TestApplication.Password);
        ana.Session.Dispose();

        Assert.NotNull(RecoveryCode.Read(code));
        Assert.True(_app.ProfileService.List().Single().HasPassword);
        Assert.False(TestApplication.DiskHolds(_app.DataFolder.ProfileDatabasePath(ana.Id), "Groceries"));
        Assert.Equal(1, _app.Open(ana.Id, TestApplication.Password).Notes.List(new NoteQuery()).Total);
    }

    [Fact]
    public void AProfileWithAPasswordOpensOnlyWithIt()
    {
        long id = _app.CreateProfile(password: TestApplication.Password).Id;

        Assert.Equal(ErrorCodes.WrongPassword, Assert.Throws<WrongPasswordException>(() => _app.ProfileService.Open(id, "not the password")).Code);
        Assert.Equal("password", Assert.Throws<ValidationException>(() => _app.ProfileService.Open(id)).Field);
    }

    [Fact]
    public void APasswordIsAcceptedFromEightToSeventyTwoCharacters()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile();

        Assert.Equal("password", Assert.Throws<ValidationException>(() => _app.ProfileService.SetPassword(ana.Session, "short")).Field);
        Assert.Equal("password", Assert.Throws<ValidationException>(() => _app.ProfileService.SetPassword(ana.Session, new string('x', 73))).Field);
        Assert.False(ana.Session.Profile.HasPassword);
    }

    [Fact]
    public void ChangingThePasswordKeepsTheRecoveryCode()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile();
        string code = _app.ProfileService.SetPassword(ana.Session, TestApplication.Password);

        _app.ProfileService.ChangePassword(ana.Session, TestApplication.Password, "another long password");

        Assert.Throws<WrongPasswordException>(() => _app.ProfileService.Open(ana.Id, TestApplication.Password));
        _app.ProfileService.Open(ana.Id, "another long password").Dispose();
        _app.ProfileService.ResetPassword(ana.Id, code, "a third long password");
    }

    [Fact]
    public void ChangingThePasswordAsksForTheCurrentOne()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile(password: TestApplication.Password);

        Assert.Throws<WrongPasswordException>(() => _app.ProfileService.ChangePassword(ana.Session, "not the password", "another long password"));
    }

    [Fact]
    public void TheRecoveryCodeSetsANewPasswordAndIsReplaced()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile();
        ana.Notes.Create("A note");
        string code = _app.ProfileService.SetPassword(ana.Session, TestApplication.Password);
        ana.Session.Dispose();

        string newCode = _app.ProfileService.ResetPassword(ana.Id, code.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal), "a brand new password");

        Assert.NotEqual(code, newCode);
        Assert.Throws<WrongPasswordException>(() => _app.ProfileService.Open(ana.Id, TestApplication.Password));
        Assert.Equal(1, _app.Open(ana.Id, "a brand new password").Notes.List(new NoteQuery()).Total);
        Assert.Equal(ErrorCodes.WrongRecoveryCode, Assert.Throws<WrongPasswordException>(() => _app.ProfileService.ResetPassword(ana.Id, code, "yet another password")).Code);
        _app.ProfileService.ResetPassword(ana.Id, newCode, "yet another password");
    }

    [Fact]
    public void ChangingThePasswordOrUsingTheRecoveryCodeKeepsWindowsHello()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile();
        string code = _app.ProfileService.SetPassword(ana.Session, TestApplication.Password);
        string helloKey = TurnOnHelloByHand(ana.Id);

        _app.ProfileService.ChangePassword(ana.Session, TestApplication.Password, "another long password");
        _app.ProfileService.ResetPassword(ana.Id, code, "a third long password");

        Assert.Equal(helloKey, _app.Profiles.FindHelloKey(ana.Id));
        Assert.True(_app.ProfileService.List().Single().HasHello);
    }

    [Fact]
    public async Task TurningOnWindowsHelloAsksForThePassword()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile(password: TestApplication.Password);

        await Assert.ThrowsAsync<WrongPasswordException>(() => _app.ProfileService.TurnOnHelloAsync(ana.Session, "not the password"));
        Assert.False(_app.ProfileService.List().Single().HasHello);
    }

    [Fact]
    public async Task WindowsHelloIsNotOfferedToAProfileWithoutAPassword()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile();

        await Assert.ThrowsAsync<WrongPasswordException>(() => _app.ProfileService.TurnOnHelloAsync(ana.Session, TestApplication.Password));
    }

    [Fact]
    public async Task AProfileWithoutWindowsHelloDoesNotOpenInPlaceOfItsPassword()
    {
        long id = _app.CreateProfile(password: TestApplication.Password).Id;

        var failure = await Assert.ThrowsAsync<HelloException>(() => _app.ProfileService.OpenWithHelloAsync(id));

        Assert.Equal(ErrorCodes.HelloKeyLost, failure.Code);
    }

    [Fact]
    public void TheCatalogKeepsNoWindowsHelloForAProfileWithoutAPassword()
    {
        long id = _app.CreateProfile().Id;

        Assert.ThrowsAny<SqliteException>(() => _app.Profiles.SetHelloKey(id, "hello-rsa$a$b$c$d$e$f"));
    }

    [Fact]
    public async Task GettingBackInWithWindowsHelloChecksTheNewPasswordFirst()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile(password: TestApplication.Password);
        TurnOnHelloByHand(ana.Id);

        var failure = await Assert.ThrowsAsync<ValidationException>(() => _app.ProfileService.ResetPasswordWithHelloAsync(ana.Id, "short"));

        Assert.Equal("password", failure.Field);
    }

    [Fact]
    public async Task AProfileWithoutWindowsHelloIsNotOpenedWithIt()
    {
        long id = _app.CreateProfile(password: TestApplication.Password).Id;

        var failure = await Assert.ThrowsAsync<HelloException>(() => _app.ProfileService.ResetPasswordWithHelloAsync(id, "a brand new password"));

        Assert.Equal(ErrorCodes.HelloKeyLost, failure.Code);
        _app.ProfileService.Open(id, TestApplication.Password).Dispose();
    }

    [Theory]
    [InlineData("not a code")]
    [InlineData("0000-0000-0000-0000-0000-0000")]
    public void AWrongRecoveryCodeOpensNothing(string code)
    {
        long id = _app.CreateProfile(password: TestApplication.Password).Id;

        var failure = Assert.Throws<WrongPasswordException>(() => _app.ProfileService.ResetPassword(id, code, "a brand new password"));

        Assert.Equal(ErrorCodes.WrongRecoveryCode, failure.Code);
    }

    [Fact]
    public void SettingAPasswordThatWasCutShortIsFinishedWhenTheProfileOpens()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile();
        long noteId = ana.Notes.Create("Groceries for the weekend").Id;
        ana.Attachments.Add(noteId, "list.txt", "text/plain", Encoding.UTF8.GetBytes("Bread and milk"));
        ana.Session.Dispose();

        // The keys were kept, and nothing after that happened.
        byte[] key = KeyWrapper.CreateKey();
        _app.Profiles.SetKeys(ana.Id, KeyWrapper.Wrap(key, TestApplication.Password), KeyWrapper.Wrap(key, RecoveryCode.Read(RecoveryCode.Create())!));

        TestApplication.OpenProfile again = _app.Open(ana.Id, TestApplication.Password);

        Assert.Equal(1, again.Notes.List(new NoteQuery()).Total);
        Assert.False(TestApplication.DiskHolds(_app.DataFolder.UploadsFolder(ana.Id), "Bread and milk"));
        again.Session.Dispose();
        Assert.False(TestApplication.DiskHolds(_app.DataFolder.ProfileDatabasePath(ana.Id), "Groceries"));
    }

    [Fact]
    public void AnOpenProfileIsRenamedAndCarriesItsNewName()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile("Ana");
        _app.ProfileService.Create("Zoe");

        Assert.Equal("Ana María", _app.ProfileService.Rename(ana.Session, " Ana María ").Name);
        Assert.Equal("Ana María", ana.Session.Profile.Name);
        Assert.Equal(ErrorCodes.ProfileExists, Assert.Throws<ConflictException>(() => _app.ProfileService.Rename(ana.Session, "zoe")).Code);
        Assert.Equal("Ana María", ana.Session.Profile.Name);
    }

    [Fact]
    public void DeletingAProfileTakesEverythingItHad()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile(password: TestApplication.Password);

        _app.ProfileService.Delete(ana.Session, TestApplication.Password);

        Assert.Empty(_app.ProfileService.List());
        Assert.False(Directory.Exists(_app.DataFolder.ProfileFolder(ana.Id)));
    }

    [Fact]
    public void DeletingAProfileWithAPasswordAsksForIt()
    {
        TestApplication.OpenProfile ana = _app.CreateProfile(password: TestApplication.Password);

        Assert.Throws<WrongPasswordException>(() => _app.ProfileService.Delete(ana.Session, "not the password"));
        Assert.Single(_app.ProfileService.List());
    }

    [Fact]
    public void AProfileThatDoesNotExistIsNotFound()
    {
        Assert.Throws<NotFoundException>(() => _app.ProfileService.Open(999));
    }

    /// <summary>
    /// Keeps a wrapping by Windows Hello for a profile, as turning Hello on would, with a signature
    /// no Hello gave: the tests run with no person to answer Hello.
    /// </summary>
    private string TurnOnHelloByHand(long id)
    {
        (string credential, byte[] challenge) = HelloKey.Begin();
        string helloKey = HelloKey.Wrap(KeyWrapper.CreateKey(), credential, challenge, RandomNumberGenerator.GetBytes(256));
        _app.Profiles.SetHelloKey(id, helloKey);
        return helloKey;
    }
}
