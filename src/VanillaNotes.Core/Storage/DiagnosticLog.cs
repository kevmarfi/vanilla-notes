using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace VanillaNotes.Core.Storage;

/// <summary>
/// The record of the failures nothing expected, kept on this machine so that a person who reports
/// a fault can hand over what happened. It holds only those failures, each with the moment and the
/// trace of the code it came from, never a note, a password, or a key, and the folder of the Windows
/// account is written in its short form so the name of the account does not travel with it. It is
/// never sent anywhere: the person copies it and decides. Unlike the rest of the storage, it does not
/// let a failure of the disk through: a record that cannot be written is dropped, since the failure
/// it was recording is already being handled and must not be replaced by another.
/// </summary>
public sealed class DiagnosticLog
{
    /// <summary>The name of the file the failures are written to.</summary>
    private const string _currentFile = "diagnostics.log";

    /// <summary>The name the file takes when it grows full, replacing the one that had it before.</summary>
    private const string _previousFile = "diagnostics.old.log";

    /// <summary>How large the file may grow before it is set aside and a new one started: one megabyte.</summary>
    private const long _maxFileBytes = 1024 * 1024;

    /// <summary>The short form of the folder of the Windows account, as Windows itself writes it.</summary>
    private const string _accountFolderShort = "%USERPROFILE%";

    /// <summary>The line that stands for the record when there is no failure in it.</summary>
    private const string _noFailures = "No failures recorded.";

    /// <summary>The folder of the Windows account, written in its short form wherever a failure names it.</summary>
    private readonly string _accountFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Keeps two failures that happen at once on different threads from writing over each other.</summary>
    private readonly Lock _writing = new();

    public DiagnosticLog(string folder)
    {
        Folder = Path.GetFullPath(folder);
    }

    /// <summary>The folder the record is kept in.</summary>
    public string Folder { get; }

    private string CurrentPath => Path.Combine(Folder, _currentFile);

    private string PreviousPath => Path.Combine(Folder, _previousFile);

    // -- Reads -- //

    /// <summary>
    /// What a person hands over when reporting a fault: the release of the application, the version of
    /// Windows and of .NET, and every failure recorded, oldest first.
    /// </summary>
    public string Report()
    {
        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"Vanilla Notes {ApplicationVersion()}");
        report.AppendLine(CultureInfo.InvariantCulture, $"{RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");
        report.AppendLine(RuntimeInformation.FrameworkDescription);
        report.AppendLine();

        string failures;
        lock (_writing)
        {
            failures = ReadIfThere(PreviousPath) + ReadIfThere(CurrentPath);
        }

        report.Append(failures.Length == 0 ? _noFailures : failures);
        return report.ToString();
    }

    // -- Writes -- //

    /// <summary>
    /// Writes down a failure nothing expected, with the moment it happened. A file grown full is set
    /// aside first, so the record never takes more than two files of the disk.
    /// </summary>
    public void Record(Exception failure)
    {
        string entry = $"[{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}] {Shorten(failure.ToString())}{Environment.NewLine}{Environment.NewLine}";
        lock (_writing)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var current = new FileInfo(CurrentPath);
                if (current.Exists && current.Length >= _maxFileBytes)
                {
                    File.Move(CurrentPath, PreviousPath, overwrite: true);
                }

                File.AppendAllText(CurrentPath, entry);
            }
            catch (Exception unwritten) when (unwritten is IOException or UnauthorizedAccessException)
            {
                // The record is dropped: see the summary of the class.
            }
        }
    }

    private static string ApplicationVersion() =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    private static string ReadIfThere(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        catch (Exception unread) when (unread is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    private string Shorten(string text) =>
        _accountFolder.Length == 0 ? text : text.Replace(_accountFolder, _accountFolderShort, StringComparison.OrdinalIgnoreCase);
}
