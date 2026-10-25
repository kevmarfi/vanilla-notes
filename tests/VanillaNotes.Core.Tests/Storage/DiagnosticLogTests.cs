using System.Runtime.InteropServices;
using VanillaNotes.Core.Storage;
using Xunit;

namespace VanillaNotes.Core.Tests.Storage;

/// <summary>The record of failures nothing expected, and the report a person copies from it.</summary>
public sealed class DiagnosticLogTests : IDisposable
{
    /// <summary>More than the size at which the record sets its file aside.</summary>
    private const int _overFullBytes = 1024 * 1024 + 1;

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("vanilla-notes-tests-");

    private readonly DiagnosticLog _log;

    public DiagnosticLogTests()
    {
        _log = new DiagnosticLog(Path.Combine(_folder.FullName, "logs"));
    }

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public void AReportWithNoFailureSaysSo()
    {
        Assert.Contains("No failures recorded.", _log.Report(), StringComparison.Ordinal);
    }

    [Fact]
    public void ARecordedFailureIsInTheReportWithItsKindAndMessage()
    {
        _log.Record(new InvalidOperationException("the first failure"));

        string report = _log.Report();

        Assert.Contains("System.InvalidOperationException: the first failure", report, StringComparison.Ordinal);
        Assert.DoesNotContain("No failures recorded.", report, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReportNamesTheVersionOfWindowsAndOfDotNet()
    {
        string report = _log.Report();

        Assert.Contains(RuntimeInformation.FrameworkDescription, report, StringComparison.Ordinal);
        Assert.Contains(RuntimeInformation.OSDescription, report, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFolderOfTheWindowsAccountIsWrittenInItsShortForm()
    {
        string account = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        _log.Record(new IOException($"Could not read {account}\\notes.txt"));

        string report = _log.Report();
        Assert.Contains("%USERPROFILE%\\notes.txt", report, StringComparison.Ordinal);
        Assert.DoesNotContain(account, report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFullFileIsSetAsideAndTheOlderFailuresStayInTheReport()
    {
        _log.Record(new InvalidOperationException("the old failure"));
        File.AppendAllText(Path.Combine(_log.Folder, "diagnostics.log"), new string('x', _overFullBytes));

        _log.Record(new InvalidOperationException("the new failure"));

        string report = _log.Report();
        Assert.True(report.IndexOf("the old failure", StringComparison.Ordinal) < report.IndexOf("the new failure", StringComparison.Ordinal));
        Assert.Equal(["diagnostics.log", "diagnostics.old.log"], Directory.GetFiles(_log.Folder).Select(Path.GetFileName).Order().ToArray());
    }

    [Fact]
    public void OnlyTwoFilesAreEverKept()
    {
        for (int round = 0; round < 3; round++)
        {
            _log.Record(new InvalidOperationException($"round {round}"));
            File.AppendAllText(Path.Combine(_log.Folder, "diagnostics.log"), new string('x', _overFullBytes));
        }

        _log.Record(new InvalidOperationException("the last failure"));

        string report = _log.Report();
        Assert.Equal(2, Directory.GetFiles(_log.Folder).Length);
        Assert.DoesNotContain("round 0", report, StringComparison.Ordinal);
        Assert.Contains("the last failure", report, StringComparison.Ordinal);
    }

    [Fact]
    public void AFailureThatCannotBeWrittenIsDroppedWithoutAFailureOfItsOwn()
    {
        // A file where the folder of the record should be: nothing can be written under it.
        string blocked = Path.Combine(_folder.FullName, "blocked");
        File.WriteAllText(blocked, "");
        var log = new DiagnosticLog(blocked);

        log.Record(new InvalidOperationException("lost"));

        Assert.Contains("No failures recorded.", log.Report(), StringComparison.Ordinal);
    }
}
