using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VanillaNotes.App.Converters;

/// <summary>
/// Whether a part of a view is shown, from a value of its view model: a yes or no, or a number,
/// which counts as yes when it is zero, for an empty list or an empty box. Each use says what
/// yes and no become.
/// </summary>
public sealed class VisibilityConverter : IValueConverter
{
    /// <summary>What the part becomes when the value is yes, or a number that is zero.</summary>
    public Visibility WhenTrue { get; set; } = Visibility.Visible;

    /// <summary>What the part becomes otherwise.</summary>
    public Visibility WhenFalse { get; set; } = Visibility.Collapsed;

    /// <summary>The visibility of the part for a value.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            bool yes => yes ? WhenTrue : WhenFalse,
            int number => number == 0 ? WhenTrue : WhenFalse,
            _ => WhenFalse,
        };

    /// <summary>A visibility is never read back as a value.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
