using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VanillaNotes.App.Controls;

/// <summary>
/// A dialog over the page of the window: a shade over everything behind it, and the card of the
/// dialog in the middle. When it appears, the keyboard goes to the first thing of the dialog that
/// takes it, and the tab key goes round the dialog without leaving it. A screen reader is told it
/// is a pane of its own, named by the heading of the dialog, or by its first text when it has no
/// heading, as a message has none.
/// </summary>
public sealed class DialogLayer : ContentControl
{
    /// <summary>The key of the style of the heading of a dialog, which gives the dialog its name.</summary>
    private const string _headingStyle = "Heading";

    public DialogLayer()
    {
        Loaded += (_, _) => MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DialogPeer(this);

    /// <summary>The text that names the dialog: its heading, or its first text; nothing when it shows no text.</summary>
    private string Title()
    {
        object? headingStyle = TryFindResource(_headingStyle);
        List<TextBlock> texts = [.. ShownTexts(this)];
        TextBlock? named = texts.FirstOrDefault(text => text.Style == headingStyle) ?? texts.FirstOrDefault();
        return named?.Text ?? "";
    }

    /// <summary>The texts of an element that are shown and say something, in the order they are drawn.</summary>
    private static IEnumerable<TextBlock> ShownTexts(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is UIElement { IsVisible: false })
            {
                continue;
            }

            if (child is TextBlock text && !string.IsNullOrWhiteSpace(text.Text))
            {
                yield return text;
            }

            foreach (TextBlock inner in ShownTexts(child))
            {
                yield return inner;
            }
        }
    }

    /// <summary>What a screen reader is told of a dialog: a pane, with the name of its heading.</summary>
    private sealed class DialogPeer(DialogLayer layer) : FrameworkElementAutomationPeer(layer)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override string GetClassNameCore() => nameof(DialogLayer);

        // The name the base gives is the content as text, which for a dialog is the name of its view model.
        protected override string GetNameCore()
        {
            string name = AutomationProperties.GetName(layer);
            return string.IsNullOrEmpty(name) ? layer.Title() : name;
        }
    }
}
