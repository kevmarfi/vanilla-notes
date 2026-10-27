using VanillaNotes.Core.Errors;

namespace VanillaNotes.App.Services;

/// <summary>
/// The text of a failure, by its code, in the language of the interface. It is the one place a
/// failure becomes words: a view model holds the failure, and shows what this gives for it.
/// </summary>
public sealed class FailureMessages
{
    /// <summary>The key of the text shown for a failure the application did not expect.</summary>
    public const string UnexpectedKey = "error.UNEXPECTED";

    private readonly LanguageService _texts;

    public FailureMessages(LanguageService texts)
    {
        _texts = texts;
    }

    /// <summary>
    /// The text of a failure of the application. A field that was not accepted has a text of its
    /// own; a code with no text gets the one of an unexpected failure.
    /// </summary>
    public string Text(AppException failure)
    {
        if (failure is ValidationException invalid && Has($"error.{failure.Code}.{invalid.Field}"))
        {
            return _texts[$"error.{failure.Code}.{invalid.Field}"];
        }

        return Has($"error.{failure.Code}") ? _texts[$"error.{failure.Code}"] : _texts[UnexpectedKey];
    }

    /// <summary>The text of a failure the application did not expect: that something went wrong, and nothing more.</summary>
    public string Unexpected => _texts[UnexpectedKey];

    private bool Has(string key) => _texts[key] != key;
}
