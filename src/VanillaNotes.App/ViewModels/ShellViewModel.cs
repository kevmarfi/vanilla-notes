using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VanillaNotes.App.ViewModels;

/// <summary>
/// The one window of the application: the page it shows, the window of profiles or the
/// workspace, and the dialogs open over it, the last one in front. A page is replaced, never
/// stacked; while a dialog is open, the page takes nothing the person does, as under the dialogs of
/// the web version, and only the dialog in front is seen.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    /// <summary>The page shown.</summary>
    [ObservableProperty]
    private object? _page;

    /// <summary>Whether no dialog is open, so that the page takes what the person does.</summary>
    [ObservableProperty]
    private bool _isPageActive = true;

    /// <summary>The dialogs open, in the order they were opened.</summary>
    public ObservableCollection<DialogLayerViewModel> Dialogs { get; } = [];

    /// <summary>Puts a dialog in front of the others.</summary>
    public void Push(DialogLayerViewModel layer)
    {
        if (Dialogs.Count > 0)
        {
            Dialogs[^1].IsFront = false;
        }

        Dialogs.Add(layer);
        IsPageActive = false;
    }

    /// <summary>Takes a dialog away, and gives the front to the one behind it.</summary>
    public void Remove(DialogLayerViewModel layer)
    {
        Dialogs.Remove(layer);
        if (Dialogs.Count > 0)
        {
            Dialogs[^1].IsFront = true;
        }

        IsPageActive = Dialogs.Count == 0;
    }
}
