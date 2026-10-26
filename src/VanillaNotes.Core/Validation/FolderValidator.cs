using VanillaNotes.Core.Errors;

namespace VanillaNotes.Core.Validation;

/// <summary>What the name of a folder accepts.</summary>
public static class FolderValidator
{
    /// <summary>The longest a name of a folder may be.</summary>
    public const int MaxNameLength = 100;

    /// <summary>
    /// Accepts the name of a folder: 1 to 100 characters once the spaces around it are taken away.
    /// </summary>
    /// <returns>The name without the spaces around it.</returns>
    public static string Name(string? name)
    {
        string trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            throw new ValidationException("name", "Name is required");
        }

        if (trimmed.Length > MaxNameLength)
        {
            throw new ValidationException("name", $"Name must be at most {MaxNameLength} characters long");
        }

        return trimmed;
    }
}
