using System.Text.Json;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Storage;

/// <summary>
/// Where the window was on the screens of this machine when it was last closed, kept in a small
/// file of its own. It belongs to the machine and its screens, not to a profile, so it lives in the
/// local application data of the Windows account and no backup carries it. Unlike the rest of the
/// storage, it lets no failure of the disk through: a file that cannot be read, or holds something
/// else, only means the window opens where it would the first time; and one that cannot be written
/// only means the window forgets.
/// </summary>
public sealed class WindowPlacementFile
{
    /// <summary>The name of the file, inside the folder of the application in the local application data.</summary>
    private const string _fileName = "window.json";

    /// <summary>How the file is written and read: by the names of its fields, starting in lower case.</summary>
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _path;

    public WindowPlacementFile(string folder)
    {
        _path = Path.Combine(Path.GetFullPath(folder), _fileName);
    }

    // -- Reads -- //

    /// <summary>Where the window was, or nothing when it never closed or the file says nothing a window can take.</summary>
    public WindowPlacement? Find()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            WindowPlacement? placement = JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(_path), _json);
            return placement is not null && IsUsable(placement) ? placement : null;
        }
        catch (Exception unread) when (unread is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    // -- Writes -- //

    /// <summary>Keeps where the window is, in place of where it was.</summary>
    public void Save(WindowPlacement placement)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(placement, _json));
        }
        catch (Exception unwritten) when (unwritten is IOException or UnauthorizedAccessException)
        {
            // The window forgets: see the summary of the class.
        }
    }

    private static bool IsUsable(WindowPlacement placement) =>
        double.IsFinite(placement.Left) && double.IsFinite(placement.Top) && placement.Width > 0 && placement.Height > 0
        && double.IsFinite(placement.Width) && double.IsFinite(placement.Height);
}
