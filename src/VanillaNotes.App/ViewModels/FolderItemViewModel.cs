using CommunityToolkit.Mvvm.ComponentModel;
using VanillaNotes.Core.Models;

namespace VanillaNotes.App.ViewModels;

/// <summary>One folder of the sidebar: its name, how many notes it holds, and its name while it is being changed.</summary>
public sealed partial class FolderItemViewModel : ObservableObject
{
    /// <summary>Whether the name of the folder is being changed in its row.</summary>
    [ObservableProperty]
    private bool _isRenaming;

    /// <summary>The name typed while it is being changed.</summary>
    [ObservableProperty]
    private string _newName = "";

    public FolderItemViewModel(Folder folder)
    {
        Folder = folder;
    }

    /// <summary>The folder as the service gave it.</summary>
    public Folder Folder { get; }

    /// <summary>The number of the folder.</summary>
    public long Id => Folder.Id;

    /// <summary>The name of the folder.</summary>
    public string Name => Folder.Name;

    /// <summary>How many notes the folder holds.</summary>
    public int NoteCount => Folder.NoteCount;
}
