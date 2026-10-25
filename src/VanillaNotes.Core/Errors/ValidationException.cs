namespace VanillaNotes.Core.Errors;

/// <summary>
/// What the person entered was not accepted. It names the one field at fault, so that the
/// interface can point at it; the first rule a field breaks is the one reported.
/// </summary>
public sealed class ValidationException : AppException
{
    public ValidationException(string field, string message) : base(ErrorCodes.Validation, $"{field}: {message}") => Field = field;

    /// <summary>The field that was not accepted, by the name the validator gives it.</summary>
    public string Field { get; }
}
