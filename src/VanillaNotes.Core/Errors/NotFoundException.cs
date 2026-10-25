namespace VanillaNotes.Core.Errors;

/// <summary>The thing asked for is not there: it was never made, or it was deleted.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(ErrorCodes.NotFound, message) { }
}
