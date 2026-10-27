using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VanillaNotes.App.Services;

namespace VanillaNotes.App.ViewModels;

/// <summary>
/// The recovery code of a profile, shown once when its password is set: to copy, to save as a
/// file, or to send by the mail program of the person, which leaves it in a mailbox as plain text.
/// </summary>
public sealed partial class RecoveryCodeViewModel : ObservableObject
{
    private readonly string _profileName;

    private readonly DialogService _dialogs;

    private readonly LanguageService _texts;

    /// <summary>What was last done with the code, said under it.</summary>
    [ObservableProperty]
    private string _status = "";

    public RecoveryCodeViewModel(string profileName, string code, DialogService dialogs, LanguageService texts)
    {
        _profileName = profileName;
        Code = code;
        _dialogs = dialogs;
        _texts = texts;
    }

    /// <summary>The code, in groups of four.</summary>
    public string Code { get; }

    private string Sentence => _texts.Format("code.fileText", ("name", _profileName), ("code", Code));

    /// <summary>Puts the code on the clipboard.</summary>
    [RelayCommand]
    private void Copy()
    {
        _dialogs.CopyText(Code);
        Status = _texts["code.copied"];
    }

    /// <summary>Saves the code in a file the person picks.</summary>
    [RelayCommand]
    private void Save()
    {
        if (_dialogs.SaveText(_texts["code.fileName"], Sentence))
        {
            Status = _texts["code.saved"];
        }
    }

    /// <summary>Opens the mail program with the code written in a new message, once the person has been told what that means.</summary>
    [RelayCommand]
    private void Email()
    {
        if (_dialogs.Confirm(_texts["code.emailWarning"], "code.email"))
        {
            _dialogs.OpenMail(_texts["code.emailSubject"], Sentence);
        }
    }

    /// <summary>Closes the dialog: the code is not shown again.</summary>
    [RelayCommand]
    private void Done() => _dialogs.Close(this, done: true);
}
