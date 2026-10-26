using VanillaNotes.Core.Errors;

namespace VanillaNotes.Core.Validation;

/// <summary>What the name and the password of a profile accept.</summary>
public static class ProfileValidator
{
    /// <summary>The longest a name of a profile may be.</summary>
    public const int MaxNameLength = 50;

    /// <summary>The shortest a password may be, as in the web version.</summary>
    public const int MinPasswordLength = 8;

    /// <summary>The longest a password may be, as in the web version.</summary>
    public const int MaxPasswordLength = 72;

    /// <summary>
    /// Accepts the name of a profile: 1 to 50 characters once the spaces around it are taken away.
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

    /// <summary>
    /// Accepts a new password: 8 to 72 characters, taken as they are, spaces included.
    /// </summary>
    public static string NewPassword(string? password)
    {
        if (password is null || password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
        {
            throw new ValidationException("password", $"Password must be between {MinPasswordLength} and {MaxPasswordLength} characters long");
        }

        return password;
    }

    /// <summary>
    /// Accepts the password given to open a profile, or to prove it is the person's: anything
    /// but nothing, since whether it is right is for the key to say.
    /// </summary>
    public static string GivenPassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ValidationException("password", "Password is required");
        }

        return password;
    }
}
