namespace VanillaNotes.Core.Errors;

/// <summary>
/// Windows Hello did not open the profile: it is not set up on this machine, the person closed
/// its window, or it no longer opens the profile.
/// </summary>
public sealed class HelloException : AppException
{
    public HelloException(string code, string message) : base(code, message) { }
}
