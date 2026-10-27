using System.Globalization;
using System.Windows;
using System.Windows.Data;
using VanillaNotes.App.Services;

namespace VanillaNotes.App.Converters;

/// <summary>
/// A date as the list of notes writes it, after the setting of dates: relative to now, as the
/// web version writes it, or the exact day and time.
/// </summary>
public sealed class DateTextConverter : IValueConverter
{
    /// <summary>The setting of dates that writes the exact day and time.</summary>
    private const string _exact = "exact";

    /// <summary>
    /// A date as text: "just now", minutes, hours, or days ago within a week, and the day after
    /// that; or, with the exact style, the day and the time.
    /// </summary>
    /// <param name="style">The setting of dates: relative or exact.</param>
    public static string Describe(DateTimeOffset date, string style, LanguageService texts)
    {
        CultureInfo culture = texts.Culture;
        DateTime local = date.LocalDateTime;
        if (style == _exact)
        {
            return $"{local.ToString("d MMM yyyy", culture)}, {local.ToString("t", culture)}";
        }

        TimeSpan ago = DateTimeOffset.UtcNow - date;
        if (ago < TimeSpan.FromMinutes(1))
        {
            return texts["list.justNow"];
        }

        if (ago < TimeSpan.FromHours(1))
        {
            return texts.Count("date.minute", (long)ago.TotalMinutes);
        }

        if (ago < TimeSpan.FromDays(1))
        {
            return texts.Count("date.hour", (long)ago.TotalHours);
        }

        return ago < TimeSpan.FromDays(7) ? texts.Count("date.day", (long)ago.TotalDays) : local.ToString("d MMM yyyy", culture);
    }

    /// <summary>A date bound in a view, with the setting of dates as the parameter.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is DateTimeOffset date && Application.Current.TryFindResource("Texts") is LanguageService texts
            ? Describe(date, parameter as string ?? "relative", texts)
            : "";

    /// <summary>A date is never read back from its text.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
