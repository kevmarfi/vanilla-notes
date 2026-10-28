namespace VanillaNotes.Core.Errors;

/// <summary>
/// Every failure code of the application. A code names what went wrong, and is the key the
/// interface looks the message up by; the list, with when each one happens, is kept in the
/// document of the errors of the architecture.
/// </summary>
public static class ErrorCodes
{
    /// <summary>A field of what the person entered is missing or breaks one of its rules.</summary>
    public const string Validation = "VALIDATION_ERROR";

    /// <summary>The note, folder, attachment, or profile asked for does not exist.</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>Another folder of the profile already has that name.</summary>
    public const string FolderExists = "FOLDER_EXISTS";

    /// <summary>Another profile of this machine already has that name.</summary>
    public const string ProfileExists = "PROFILE_EXISTS";

    /// <summary>The note changed since it was read, so saving it would overwrite the change.</summary>
    public const string VersionConflict = "VERSION_CONFLICT";

    /// <summary>The password given does not open the profile.</summary>
    public const string WrongPassword = "WRONG_PASSWORD";

    /// <summary>The recovery code given does not open the profile.</summary>
    public const string WrongRecoveryCode = "WRONG_RECOVERY_CODE";

    /// <summary>Windows Hello is not set up on this machine or Windows account, or Windows does not offer it.</summary>
    public const string HelloUnavailable = "HELLO_UNAVAILABLE";

    /// <summary>The person closed the window of Windows Hello without confirming who they are.</summary>
    public const string HelloCanceled = "HELLO_CANCELED";

    /// <summary>The Windows Hello of this machine no longer opens the profile, so it was turned off for it.</summary>
    public const string HelloKeyLost = "HELLO_KEY_LOST";

    /// <summary>The disk of the data is running out of space, so nothing new is stored.</summary>
    public const string StorageFull = "STORAGE_FULL";

    /// <summary>A database was written by a newer release of the application, and this one must not change it.</summary>
    public const string DatabaseTooNew = "DATABASE_TOO_NEW";

    /// <summary>The file of a database is damaged, so it is not opened and must be put back from a backup.</summary>
    public const string DatabaseDamaged = "DATABASE_DAMAGED";
}
