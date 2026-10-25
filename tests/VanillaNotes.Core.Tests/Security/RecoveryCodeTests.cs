using System.Text.RegularExpressions;
using Xunit;
using VanillaNotes.Core.Security;

namespace VanillaNotes.Core.Tests.Security;

/// <summary>The recovery code as it is shown, and as it is read back however it was typed.</summary>
public sealed partial class RecoveryCodeTests
{
    [Fact]
    public void ACodeIsSixGroupsOfFourCharactersThatAreNotMistakenForOthers()
    {
        Assert.Matches(ShownCode(), RecoveryCode.Create());
    }

    [Fact]
    public void TwoCodesAreNotTheSame()
    {
        Assert.NotEqual(RecoveryCode.Create(), RecoveryCode.Create());
    }

    [Fact]
    public void ACodeIsReadBackWithoutItsHyphens()
    {
        string shown = RecoveryCode.Create();

        Assert.Equal(shown.Replace("-", "", StringComparison.Ordinal), RecoveryCode.Read(shown));
    }

    [Fact]
    public void CapitalsSpacesAndHyphensMakeNoDifference()
    {
        string shown = RecoveryCode.Create();
        string typed = $"  {shown.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal)} ";

        Assert.Equal(RecoveryCode.Read(shown), RecoveryCode.Read(typed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("K7QF-2M9X-TR4B-8WNC-HJ3D")]
    [InlineData("K7QF-2M9X-TR4B-8WNC-HJ3D-6PVYZ")]
    [InlineData("K7QF-2M9X-TR4B-8WNC-HJ3D-6PVI")]
    [InlineData("K7QF-2M9X-TR4B-8WNC-HJ3D-6PV!")]
    public void WhatCannotBeACodeIsReadAsNothing(string typed)
    {
        Assert.Null(RecoveryCode.Read(typed));
    }

    [GeneratedRegex("^[0-9A-HJKMNP-TV-Z]{4}(-[0-9A-HJKMNP-TV-Z]{4}){5}$")]
    private static partial Regex ShownCode();
}
