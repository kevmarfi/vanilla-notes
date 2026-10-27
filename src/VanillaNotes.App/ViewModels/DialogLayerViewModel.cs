using CommunityToolkit.Mvvm.ComponentModel;

namespace VanillaNotes.App.ViewModels;

/// <summary>
/// A dialog open over the page of the window: the view model of the dialog, which chooses its
/// view, and whether it is the one in front, the only one seen and the only one that takes what
/// the person does.
/// </summary>
public sealed partial class DialogLayerViewModel : ObservableObject
{
    /// <summary>Whether this dialog is in front of the others.</summary>
    [ObservableProperty]
    private bool _isFront = true;

    public DialogLayerViewModel(object dialog)
    {
        Dialog = dialog;
    }

    /// <summary>The view model of the dialog.</summary>
    public object Dialog { get; }

    /// <summary>Whether the dialog was closed with its action done, rather than cancelled.</summary>
    public bool IsDone { get; set; }
}
