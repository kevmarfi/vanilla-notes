using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;

namespace VanillaNotes.App.Services;

/// <summary>
/// The colours of the interface: the theme a profile chose, or, until it chooses one, the light
/// or dark mode of Windows. While Windows is in high contrast, its colours are used whatever the
/// theme, and the change is followed while the application is open. A theme is one resource
/// dictionary of colours, put in place of the one in use.
/// </summary>
public sealed class ThemeService
{
    /// <summary>Where Windows keeps whether applications are shown light or dark.</summary>
    private const string _personalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>The value under that key that is zero when applications are shown dark.</summary>
    private const string _lightValue = "AppsUseLightTheme";

    /// <summary>The name of the dictionary of the colours of Windows in high contrast, which no setting can choose.</summary>
    private const string _highContrast = "highContrast";

    /// <summary>The dictionary of colours of each theme, by the name a setting gives it.</summary>
    private static readonly Dictionary<string, string> _dictionaries = new(StringComparer.Ordinal)
    {
        ["light"] = "Themes/Light.xaml",
        ["dark"] = "Themes/Dark.xaml",
        ["neutral"] = "Themes/Neutral.xaml",
        [_highContrast] = "Themes/HighContrast.xaml",
    };

    /// <summary>The theme last asked for, put back when Windows leaves high contrast.</summary>
    private string? _chosen;

    public ThemeService()
    {
        SystemParameters.StaticPropertyChanged += OnSystemChanged;
    }

    /// <summary>
    /// Shows the interface in a theme, or in the one Windows uses when none is given; in the colours
    /// of Windows instead while it is in high contrast.
    /// </summary>
    public void Apply(string? theme)
    {
        _chosen = theme;
        string name = SystemParameters.HighContrast ? _highContrast : theme ?? (WindowsIsDark() ? "dark" : "light");
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

    private void OnSystemChanged(object? sender, PropertyChangedEventArgs change)
    {
        if (change.PropertyName == nameof(SystemParameters.HighContrast))
        {
            Apply(_chosen);
        }
    }

    private static bool WindowsIsDark()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_personalizeKey);
        return key?.GetValue(_lightValue) is int light && light == 0;
    }
}
