namespace VanillaNotes.Core.Errors;

/// <summary>
/// The data on disk cannot be used as asked: the disk is running out of space, or a database
/// was written by a newer release of the application.
/// </summary>
public sealed class StorageException : AppException
{
    public StorageException(string code, string message) : base(code, message) { }
}
