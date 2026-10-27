using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VanillaNotes.App.Controls;

/// <summary>
/// A dialog over the page of the window: a shade over everything behind it, and the card of the
/// dialog in the middle. When it appears, the keyboard goes to the first thing of the dialog that
/// takes it, and the tab key goes round the dialog without leaving it.
/// </summary>
public sealed class DialogLayer : ContentControl
{
    public DialogLayer()
    {
        Loaded += (_, _) => MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }
}
