using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace VanillaNotes.App.Services;

/// <summary>
/// The colours of the interface: the theme a profile chose, or, until it chooses one, the light
/// or dark mode of Windows. While Windows is in high contrast, its colours are used whatever the
/// theme, and the change is followed while the application is open. A theme is one resource
/// dictionary of colours, put in place of the one in use. The title bar Windows draws over each
/// window is painted in the colours of the theme too, as native applications do; on a Windows that
/// cannot colour it, it keeps the colours of Windows, and in high contrast it is left to Windows.
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

    /// <summary>The attribute of a window that asks Windows to draw its title bar dark.</summary>
    private const int _useDarkMode = 20;

    /// <summary>The attribute of a window that sets the colour of its title bar, which Windows 11 alone honours.</summary>
    private const int _captionColour = 35;

    /// <summary>The attribute of a window that sets the colour of the text of its title bar, which Windows 11 alone honours.</summary>
    private const int _captionTextColour = 36;

    /// <summary>The value of a colour attribute that gives it back to Windows.</summary>
    private const int _defaultColour = unchecked((int)0xFFFFFFFF);

    /// <summary>The theme last asked for, put back when Windows leaves high contrast.</summary>
    private string? _chosen;

    public ThemeService()
    {
        SystemParameters.StaticPropertyChanged += OnSystemChanged;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((window, _) => PaintTitleBar((Window)window)));
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

        foreach (Window window in Application.Current.Windows)
        {
            PaintTitleBar(window);
        }
    }

    private void OnSystemChanged(object? sender, PropertyChangedEventArgs change)
    {
        if (change.PropertyName == nameof(SystemParameters.HighContrast))
        {
            Apply(_chosen);
        }
    }

    /// <summary>
    /// Paints the title bar of a window in the colours of the theme in use: its background as the
    /// surface, its text as the text, and its buttons light or dark to stand out on it.
    /// </summary>
    private static void PaintTitleBar(Window window)
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        bool byWindows = SystemParameters.HighContrast;
        Color surface = ColourOf("Surface");
        Color text = ColourOf("Text");
        bool dark = !byWindows && (0.299 * surface.R) + (0.587 * surface.G) + (0.114 * surface.B) < 128;
        SetAttribute(handle, _useDarkMode, dark ? 1 : 0);
        SetAttribute(handle, _captionColour, byWindows ? _defaultColour : ColourRef(surface));
        SetAttribute(handle, _captionTextColour, byWindows ? _defaultColour : ColourRef(text));
    }

    private static Color ColourOf(string key) => Application.Current.TryFindResource(key) is SolidColorBrush brush ? brush.Color : Colors.Transparent;

    /// <summary>A colour as Windows reads it in an attribute: red in the lowest byte, then green, then blue.</summary>
    private static int ColourRef(Color colour) => colour.R | (colour.G << 8) | (colour.B << 16);

    /// <summary>
    /// Sets one attribute of how Windows draws a window. A Windows that does not know the attribute
    /// refuses it, and the title bar keeps its colours, which is no failure of the application.
    /// </summary>
    private static void SetAttribute(IntPtr handle, int attribute, int value) => _ = DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    private static bool WindowsIsDark()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_personalizeKey);
        return key?.GetValue(_lightValue) is int light && light == 0;
    }
}
