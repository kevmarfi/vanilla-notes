namespace VanillaNotes.Core.Errors;

/// <summary>The secret given, the password or the recovery code, does not open the profile.</summary>
public sealed class WrongPasswordException : AppException
{
    public WrongPasswordException(string code, string message) : base(code, message) { }
}
