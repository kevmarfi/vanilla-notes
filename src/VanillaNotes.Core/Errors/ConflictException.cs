namespace VanillaNotes.Core.Errors;

/// <summary>
/// What was asked clashes with what already exists: a name that is taken, or a note that
/// changed since it was read.
/// </summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string code, string message) : base(code, message) { }
}
