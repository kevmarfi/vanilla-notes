using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace VanillaNotes.App.Controls;

/// <summary>
/// A box that takes the keyboard each time it is shown, with its text selected so that it can be
/// typed over: the box of the name of a new folder, or of a folder being renamed. Without it, a
/// person on the keyboard would open the box and still be on the row or the button that opened it.
/// A box can also be told to take the keyboard while it is already shown, such as the title of a
/// note that was just made, so that its first words replace the title a new note starts with.
/// </summary>
public static class FocusOnShow
{
    /// <summary>Whether a box takes the keyboard each time it is shown.</summary>
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.RegisterAttached(
        "IsOn",
        typeof(bool),
        typeof(FocusOnShow),
        new PropertyMetadata(false, OnChanged));

    /// <summary>A number that, each time it changes, tells a box to take the keyboard.</summary>
    public static readonly DependencyProperty RequestProperty = DependencyProperty.RegisterAttached(
        "Request",
        typeof(int),
        typeof(FocusOnShow),
        new PropertyMetadata(0, OnRequested));

    /// <summary>Whether a box takes the keyboard each time it is shown.</summary>
    public static bool GetIsOn(DependencyObject box) => (bool)box.GetValue(IsOnProperty);

    /// <summary>Sets whether a box takes the keyboard each time it is shown.</summary>
    public static void SetIsOn(DependencyObject box, bool value) => box.SetValue(IsOnProperty, value);

    /// <summary>The number that, each time it changes, tells a box to take the keyboard.</summary>
    public static int GetRequest(DependencyObject box) => (int)box.GetValue(RequestProperty);

    /// <summary>Sets the number that, each time it changes, tells a box to take the keyboard.</summary>
    public static void SetRequest(DependencyObject box, int value) => box.SetValue(RequestProperty, value);

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

        TakeKeyboard((TextBox)sender);
    }

    private static void OnRequested(DependencyObject target, DependencyPropertyChangedEventArgs change)
    {
        if (target is TextBox box)
        {
            TakeKeyboard(box);
        }
    }

    private static void TakeKeyboard(TextBox box)
    {
        // The box is laid out after it is shown; it can take the keyboard only then.
        box.Dispatcher.BeginInvoke(() =>
        {
            box.Focus();
            box.SelectAll();
        }, DispatcherPriority.Input);
    }
}
