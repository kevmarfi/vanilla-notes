using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VanillaNotes.App.Controls;

/// <summary>
/// The keys of the application, as the person presses them anywhere in the window. They are heard
/// by the window, so that they work before anything has the keyboard, and they are tried before the
/// control that has it, so that the editor of a note does not keep a key of the application for one
/// of its own (the visual editor centers a paragraph on the key that changes the way of writing).
/// A page lends its key bindings to the window this way, and a box can be given keys that take
/// the keyboard to it, as the search is. Nothing happens while a dialog is open, since the page
/// behind it is disabled.
/// </summary>
public static class Shortcuts
{
    /// <summary>Whether the key bindings of an element are heard by the whole window, ahead of the controls in it.</summary>
    public static readonly DependencyProperty IsAheadProperty = DependencyProperty.RegisterAttached(
        "IsAhead",
        typeof(bool),
        typeof(Shortcuts),
        new PropertyMetadata(false, OnChanged));

    /// <summary>
    /// The keys that take the keyboard to a box from anywhere in the window, each written as Windows
    /// writes it ("Ctrl+K") and separated by spaces, which no key has in its name.
    /// </summary>
    public static readonly DependencyProperty FocusKeysProperty = DependencyProperty.RegisterAttached(
        "FocusKeys",
        typeof(string),
        typeof(Shortcuts),
        new PropertyMetadata(null, OnChanged));

    /// <summary>What reads a key written as Windows writes it.</summary>
    private static readonly KeyGestureConverter _keyReader = new();

    /// <summary>The window that hears the keys of an element, and what it runs, while the element is shown.</summary>
    private static readonly DependencyProperty _listeningProperty = DependencyProperty.RegisterAttached(
        "Listening",
        typeof(object),
        typeof(Shortcuts));

    /// <summary>Whether the key bindings of an element are heard by the whole window.</summary>
    public static bool GetIsAhead(DependencyObject element) => (bool)element.GetValue(IsAheadProperty);

    /// <summary>Sets whether the key bindings of an element are heard by the whole window.</summary>
    public static void SetIsAhead(DependencyObject element, bool value) => element.SetValue(IsAheadProperty, value);

    /// <summary>The keys that take the keyboard to a box.</summary>
    public static string? GetFocusKeys(DependencyObject box) => (string?)box.GetValue(FocusKeysProperty);

    /// <summary>Sets the keys that take the keyboard to a box.</summary>
    public static void SetFocusKeys(DependencyObject box, string? value) => box.SetValue(FocusKeysProperty, value);

    private static void OnChanged(DependencyObject target, DependencyPropertyChangedEventArgs change)
    {
        if (target is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= Listen;
        element.Unloaded -= StopListening;
        StopListening(element, null);
        if (GetIsAhead(element) || GetFocusKeys(element) is not null)
        {
            element.Loaded += Listen;
            element.Unloaded += StopListening;
            if (element.IsLoaded)
            {
                Listen(element, null);
            }
        }
    }

    private static void Listen(object sender, RoutedEventArgs? loaded)
    {
        var element = (FrameworkElement)sender;
        StopListening(element, null);
        if (Window.GetWindow(element) is not Window window)
        {
            return;
        }

        KeyEventHandler heard = (_, key) => Run(element, key);
        window.PreviewKeyDown += heard;
        element.SetValue(_listeningProperty, new Listening(window, heard));
    }

    private static void StopListening(object sender, RoutedEventArgs? unloaded)
    {
        var element = (FrameworkElement)sender;
        if (element.GetValue(_listeningProperty) is Listening listening)
        {
            listening.Window.PreviewKeyDown -= listening.Heard;
            element.ClearValue(_listeningProperty);
        }
    }

    /// <summary>
    /// Does what the key pressed asks of an element that is shown and enabled: runs the key binding
    /// of it that matches, or takes the keyboard to it and selects its text, so it can be typed over.
    /// A key written wrong fails here, the first time a key is pressed in the window.
    /// </summary>
    private static void Run(FrameworkElement element, KeyEventArgs key)
    {
        if (key.Handled || !element.IsVisible || !element.IsEnabled)
        {
            return;
        }

        if (element is TextBox box && FocusKeysOf(box).Any(focusKey => focusKey.Matches(box, key)))
        {
            box.Focus();
            box.SelectAll();
            key.Handled = true;
            return;
        }

        if (!GetIsAhead(element))
        {
            return;
        }

        foreach (KeyBinding binding in element.InputBindings.OfType<KeyBinding>())
        {
            if (binding.Gesture.Matches(element, key) && binding.Command?.CanExecute(binding.CommandParameter) == true)
            {
                binding.Command.Execute(binding.CommandParameter);
                key.Handled = true;
                return;
            }
        }
    }

    private static IEnumerable<KeyGesture> FocusKeysOf(TextBox box) =>
        (GetFocusKeys(box) ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(written => (KeyGesture)_keyReader.ConvertFromInvariantString(written)!);

    /// <summary>The window that hears the keys of an element, and what it runs, so that it can stop.</summary>
    private sealed record Listening(Window Window, KeyEventHandler Heard);
}
