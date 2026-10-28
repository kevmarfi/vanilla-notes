using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace VanillaNotes.App.Controls;

/// <summary>
/// A box that takes the keyboard each time it is shown, with its text selected so that it can be
/// typed over: the box of the name of a new folder, or of a folder being renamed. Without it, a
/// person on the keyboard would open the box and still be on the row or the button that opened it.
/// </summary>
public static class FocusOnShow
{
    /// <summary>Whether a box takes the keyboard each time it is shown.</summary>
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.RegisterAttached(
        "IsOn",
        typeof(bool),
        typeof(FocusOnShow),
        new PropertyMetadata(false, OnChanged));

    /// <summary>Whether a box takes the keyboard each time it is shown.</summary>
    public static bool GetIsOn(DependencyObject box) => (bool)box.GetValue(IsOnProperty);

    /// <summary>Sets whether a box takes the keyboard each time it is shown.</summary>
    public static void SetIsOn(DependencyObject box, bool value) => box.SetValue(IsOnProperty, value);

    private static void OnChanged(DependencyObject target, DependencyPropertyChangedEventArgs change)
    {
        if (target is not TextBox box)
        {
            return;
        }

        box.IsVisibleChanged -= OnShown;
        if ((bool)change.NewValue)
        {
            box.IsVisibleChanged += OnShown;
        }
    }

    private static void OnShown(object sender, DependencyPropertyChangedEventArgs shown)
    {
        if (shown.NewValue is not true)
        {
            return;
        }

        var box = (TextBox)sender;
        // The box is laid out after it is shown; it can take the keyboard only then.
        box.Dispatcher.BeginInvoke(() =>
        {
            box.Focus();
            box.SelectAll();
        }, DispatcherPriority.Input);
    }
}
