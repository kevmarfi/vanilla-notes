using VanillaNotes.App.Converters;
using VanillaNotes.App.Services;
using VanillaNotes.Core.Models;

namespace VanillaNotes.App.ViewModels;

/// <summary>One backup of the data of this machine: when it was made, how much room it takes, and whether the application made it on its own.</summary>
public sealed class BackupItemViewModel
{
    public BackupItemViewModel(BackupInfo backup, LanguageService texts)
    {
        Name = backup.Name;
        DateText = DateTextConverter.Describe(backup.CreatedAt, "exact", texts);
        SizeText = texts.Size(backup.Size);
        IsAutomatic = backup.IsAutomatic;
    }

    /// <summary>The name of its folder, which tells it apart.</summary>
    public string Name { get; }

    /// <summary>The day and the time it was made.</summary>
    public string DateText { get; }

    /// <summary>The room it takes.</summary>
    public string SizeText { get; }

    /// <summary>Whether the application made it on its own, as the backup of a day.</summary>
    public bool IsAutomatic { get; }
}
