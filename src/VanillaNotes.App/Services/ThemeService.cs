using System.Windows;
using Microsoft.Win32;

namespace VanillaNotes.App.Services;

/// <summary>
/// The colours of the interface: the theme a profile chose, or, until it chooses one, the light
/// or dark mode of Windows. A theme is one resource dictionary of colours, put in place of the
/// one in use.
/// </summary>
public sealed class ThemeService
{
    /// <summary>Where Windows keeps whether applications are shown light or dark.</summary>
    private const string _personalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>The value under that key that is zero when applications are shown dark.</summary>
    private const string _lightValue = "AppsUseLightTheme";

    /// <summary>The dictionary of colours of each theme, by the name a setting gives it.</summary>
    private static readonly Dictionary<string, string> _dictionaries = new(StringComparer.Ordinal)
    {
        ["light"] = "Themes/Light.xaml",
        ["dark"] = "Themes/Dark.xaml",
        ["neutral"] = "Themes/Neutral.xaml",
    };

    /// <summary>
    /// Shows the interface in a theme, or in the one Windows uses when none is given.
    /// </summary>
    public void Apply(string? theme)
    {
        string name = theme ?? (WindowsIsDark() ? "dark" : "light");
        var colours = new ResourceDictionary { Source = new Uri(_dictionaries[name], UriKind.Relative) };
        IList<ResourceDictionary> merged = Application.Current.Resources.MergedDictionaries;
        int current = merged.ToList().FindIndex(dictionary => _dictionaries.Values.Any(path => dictionary.Source?.OriginalString.EndsWith(path, StringComparison.Ordinal) == true));
        if (current >= 0)
        {
            merged[current] = colours;
        }
        else
        {
            merged.Insert(0, colours);
        }
    }

    private static bool WindowsIsDark()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_personalizeKey);
        return key?.GetValue(_lightValue) is int light && light == 0;
    }
}
