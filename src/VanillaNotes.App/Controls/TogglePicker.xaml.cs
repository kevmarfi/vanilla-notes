using System.Collections;
using System.Windows;

namespace VanillaNotes.App.Controls;

/// <summary>A row of buttons for the values of one choice: each option is a key and the text shown for it.</summary>
public partial class TogglePicker
{
    /// <summary>The options, as pairs of a key and the text shown for it.</summary>
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(IEnumerable), typeof(TogglePicker));

    /// <summary>The key of the option chosen, bound in both directions.</summary>
    public static readonly DependencyProperty SelectedKeyProperty = DependencyProperty.Register(
        nameof(SelectedKey), typeof(string), typeof(TogglePicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public TogglePicker()
    {
        InitializeComponent();
    }

    /// <summary>The options.</summary>
    public IEnumerable? Options
    {
        get => (IEnumerable?)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>The key of the option chosen.</summary>
    public string? SelectedKey
    {
        get => (string?)GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }
}
