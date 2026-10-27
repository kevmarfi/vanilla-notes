using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VanillaNotes.App.Converters;

/// <summary>
/// The colors of a tag, worked out from its name as tag-chip.js of the web version works them
/// out: the same name always takes the same of five pairs of colors, and the color is stored
/// nowhere.
/// </summary>
public sealed class TagColorConverter : IValueConverter
{
    /// <summary>How many pairs of colors a tag can take.</summary>
    private const int _hueCount = 5;

    /// <summary>
    /// The pair of colors a tag takes, from one to five: the sum of the code points of its name.
    /// </summary>
    public static int Hue(string name)
    {
        int sum = 0;
        for (int i = 0; i < name.Length; i += char.IsSurrogatePair(name, i) ? 2 : 1)
        {
            sum += char.ConvertToUtf32(name, i);
        }

        return (sum % _hueCount) + 1;
    }

    /// <summary>
    /// The background or the text color of a tag, by its name; the parameter says which: "Bg" or "Text".
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string name && Application.Current.TryFindResource($"Tag{Hue(name)}{parameter}") is Brush brush
            ? brush
            : Brushes.Transparent;

    /// <summary>A color is never read back as a name.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
