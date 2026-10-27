using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace VanillaNotes.App.Services;

/// <summary>
/// The language of the interface: it picks it, and gives every text of the interface in it, by
/// its key. A view reaches a text through it, by the key between brackets, so that every text on
/// screen changes the moment the language does.
/// </summary>
public sealed class LanguageService : INotifyPropertyChanged
{
    /// <summary>The language of a person who never chose one, and of any text another language lacks.</summary>
    public const string DefaultLanguage = "en";

    /// <summary>The bytes of a kilobyte, as the web version counts them.</summary>
    private const long _bytesPerUnit = 1024;

    /// <summary>The texts of the interface, in English and in every language that translates them.</summary>
    private static readonly ResourceManager _texts = new("VanillaNotes.App.Resources.Strings", typeof(LanguageService).Assembly);

    private CultureInfo _culture = CultureInfo.GetCultureInfo(DefaultLanguage);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The language the interface is in, for formatting numbers and dates as well.</summary>
    public CultureInfo Culture => _culture;

    /// <summary>The language of Windows, when it is one the interface is written in; English when it is not.</summary>
    public static string WindowsLanguage => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : DefaultLanguage;

    /// <summary>
    /// A text of the interface in its language. A key with no text is shown as it is, so that a
    /// text that was forgotten is seen and not hidden.
    /// </summary>
    public string this[string key] => _texts.GetString(key, _culture) ?? key;

    /// <summary>
    /// A text of the interface with its places filled in: each name between braces takes the value
    /// given for it.
    /// </summary>
    public string Format(string key, params (string Name, object Value)[] values)
    {
        string text = this[key];
        foreach ((string name, object value) in values)
        {
            text = text.Replace($"{{{name}}}", Convert.ToString(value, _culture), StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>
    /// A text that says how many of something there are, in its singular or its plural form: the
    /// key with ".one" or ".other" after it, and the number in its place.
    /// </summary>
    public string Count(string key, long count) => Format(count == 1 ? $"{key}.one" : $"{key}.other", ("count", count));

    /// <summary>
    /// The size of a file as the web version writes it: in bytes, in whole kilobytes, or in
    /// megabytes with one decimal.
    /// </summary>
    public string Size(long bytes)
    {
        if (bytes < _bytesPerUnit)
        {
            return Format("size.bytes", ("size", bytes));
        }

        return bytes < _bytesPerUnit * _bytesPerUnit
            ? Format("size.kilobytes", ("size", Math.Round((double)bytes / _bytesPerUnit)))
            : Format("size.megabytes", ("size", Math.Round((double)bytes / (_bytesPerUnit * _bytesPerUnit), 1)));
    }

    /// <summary>
    /// Puts the interface in a language, and every text on screen with it.
    /// </summary>
    public void Use(string language)
    {
        _culture = CultureInfo.GetCultureInfo(language);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
    }
}
