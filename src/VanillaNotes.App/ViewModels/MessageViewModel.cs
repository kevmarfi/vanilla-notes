using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VanillaNotes.App.Services;

namespace VanillaNotes.App.ViewModels;

/// <summary>A question or a notice, in the style of the application, with one or two buttons.</summary>
public sealed partial class MessageViewModel : ObservableObject
{
    private readonly DialogService _dialogs;

    /// <summary>What the person typed, when an answer is typed.</summary>
    [ObservableProperty]
    private string _input = "";

    /// <param name="cancelText">The text of the button that does not go on, or nothing for a notice with one button.</param>
    /// <param name="isDanger">Whether going on deletes something, which its button shows.</param>
    /// <param name="hasInput">Whether the person types an answer.</param>
    public MessageViewModel(DialogService dialogs, string text, string confirmText, string? cancelText, bool isDanger, bool hasInput)
    {
        HasInput = hasInput;
        _dialogs = dialogs;
        Text = text;
        ConfirmText = confirmText;
        CancelText = cancelText ?? "";
        HasCancel = cancelText is not null;
        IsDanger = isDanger;
    }

    /// <summary>What is said or asked.</summary>
    public string Text { get; }

    /// <summary>The text of the button that goes on.</summary>
    public string ConfirmText { get; }

    /// <summary>The text of the button that does not go on.</summary>
    public string CancelText { get; }

    /// <summary>Whether there is a button that does not go on.</summary>
    public bool HasCancel { get; }

    /// <summary>Whether going on deletes something.</summary>
    public bool IsDanger { get; }

    /// <summary>Whether the person types an answer.</summary>
    public bool HasInput { get; }

    /// <summary>Goes on.</summary>
    [RelayCommand]
    private void Confirm() => _dialogs.Close(this, done: true);

    /// <summary>Does not go on.</summary>
    [RelayCommand]
    private void Cancel() => _dialogs.Close(this);
}
