namespace VanillaNotes.Core.Errors;

/// <summary>
/// Base of every failure the application reports on purpose. It carries a code, by which the
/// interface looks up the message it shows the person; its own message is for the developer
/// only and is never shown. Anything thrown that is not one of these is an unexpected failure.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string code, string message) : base(message) => Code = code;

    /// <summary>What went wrong, as one of the codes of the application.</summary>
    public string Code { get; }
}
