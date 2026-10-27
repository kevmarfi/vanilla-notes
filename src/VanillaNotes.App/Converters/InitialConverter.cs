using System.Globalization;
using System.Windows.Data;

namespace VanillaNotes.App.Converters;

/// <summary>
/// The first letter of a name, in capitals: what the round mark beside a profile shows. A letter
/// written with more than one code point, as an accented one or an emoji, is kept whole.
/// </summary>
public sealed class InitialConverter : IValueConverter
{
    /// <summary>The first letter of the name, in capitals, or nothing when the name is empty.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string name && name.Trim() is { Length: > 0 } trimmed
            ? StringInfo.GetNextTextElement(trimmed).ToUpper(culture)
            : string.Empty;

    /// <summary>A letter is never read back as a name.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
