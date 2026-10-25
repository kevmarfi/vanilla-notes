using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace VanillaNotes.Core.Storage;

/// <summary>
/// The attachment files of one profile, kept under random names in its folder of attachments.
/// It stores the bytes it is given and gives them back as they are: when the profile has a key,
/// they arrive already sealed.
/// </summary>
public sealed partial class FileStore
{
    /// <summary>How many random bytes a name is made of: 32 hexadecimal characters.</summary>
    private const int _nameBytes = 16;

    /// <summary>What the name of a file being written ends with, until it is complete.</summary>
    private const string _partialSuffix = ".partial";

    private readonly string _folder;

    public FileStore(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(folder);
    }

    /// <summary>
    /// Stores the bytes of a new file under a new random name.
    /// </summary>
    /// <returns>The name it is stored under.</returns>
    public string Save(ReadOnlySpan<byte> bytes)
    {
        string name = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(_nameBytes));

        // Fail rather than overwrite, should a name ever repeat.
        using (var file = new FileStream(PathOf(name), FileMode.CreateNew, FileAccess.Write))
        {
            file.Write(bytes);
        }

        return name;
    }

    /// <summary>
    /// The bytes of a stored file, as they were stored.
    /// </summary>
    public byte[] Read(string name) => File.ReadAllBytes(PathOf(name));

    /// <summary>
    /// Puts new bytes in place of the ones of a stored file, in one step: a reader finds the old
    /// bytes or the new ones, never half of each.
    /// </summary>
    public void Replace(string name, ReadOnlySpan<byte> bytes)
    {
        string path = PathOf(name);
        string partial = path + _partialSuffix;
        using (var file = new FileStream(partial, FileMode.Create, FileAccess.Write))
        {
            file.Write(bytes);
        }

        File.Move(partial, path, overwrite: true);
    }

    /// <summary>
    /// Deletes a stored file. A file that is not there is not a failure.
    /// </summary>
    public void Remove(string name) => File.Delete(PathOf(name));

    /// <summary>
    /// The names of every file stored, whether or not a record still points at it.
    /// </summary>
    public IReadOnlyList<string> List() =>
        Directory.EnumerateFiles(_folder)
            .Select(path => Path.GetFileName(path)!)
            .Where(name => NamePattern().IsMatch(name))
            .ToList();

    /// <summary>
    /// The path of a stored file. Only a name this store makes is accepted, so no name can
    /// reach outside its folder.
    /// </summary>
    private string PathOf(string name)
    {
        if (!NamePattern().IsMatch(name))
        {
            throw new ArgumentException("The name of a stored file is 32 hexadecimal characters.", nameof(name));
        }

        return Path.Combine(_folder, name);
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex NamePattern();
}
