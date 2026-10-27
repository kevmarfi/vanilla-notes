using System.Windows;
using System.Windows.Controls;

namespace VanillaNotes.App.Controls;

/// <summary>
/// Lets the text of a password box be bound to a view model, which the box does not allow on
/// its own. The password reaches the view model as the person types it, and is cleared from the
/// box when the view model clears it.
/// </summary>
public static class PasswordBoxBinding
{
    /// <summary>The password typed, bound in both directions. It starts as nothing, so that the first value bound reaches the box and the box starts telling the view model.</summary>
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached(
        "Password",
        typeof(string),
        typeof(PasswordBoxBinding),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordChanged));

    /// <summary>Whether a box already tells the view model when the person types.</summary>
    private static readonly DependencyProperty _isWatchedProperty = DependencyProperty.RegisterAttached(
        "IsWatched",
        typeof(bool),
        typeof(PasswordBoxBinding));

    /// <summary>The password of a box, as the binding sees it.</summary>
    public static string GetPassword(DependencyObject box) => (string?)box.GetValue(PasswordProperty) ?? "";

    /// <summary>Sets the password of a box, as the binding does.</summary>
    public static void SetPassword(DependencyObject box, string value) => box.SetValue(PasswordProperty, value);

    private static void OnPasswordChanged(DependencyObject target, DependencyPropertyChangedEventArgs change)
    {
        if (target is not PasswordBox box)
        {
            return;
        }

        if (!(bool)box.GetValue(_isWatchedProperty))
        {
            box.SetValue(_isWatchedProperty, true);
            box.PasswordChanged += (_, _) => SetPassword(box, box.Password);
        }

        string value = (string?)change.NewValue ?? "";
        if (box.Password != value)
        {
            box.Password = value;
        }
    }
}
