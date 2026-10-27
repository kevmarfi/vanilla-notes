using VanillaNotes.App.Services;
using VanillaNotes.Core.Models;

namespace VanillaNotes.App.ViewModels;

/// <summary>One file attached to the open note: its name, its size, and whether the note can show it.</summary>
public sealed class AttachmentItemViewModel
{
    public AttachmentItemViewModel(Attachment attachment, LanguageService texts)
    {
        Id = attachment.Id;
        FileName = attachment.FileName;
        IsImage = attachment.MimeType.StartsWith("image/", StringComparison.Ordinal);
        SizeText = texts.Size(attachment.Size);
    }

    /// <summary>The number of the attachment.</summary>
    public long Id { get; }

    /// <summary>The name the file had.</summary>
    public string FileName { get; }

    /// <summary>Whether it is an image, which the note can show.</summary>
    public bool IsImage { get; }

    /// <summary>Its size, in bytes, kilobytes, or megabytes.</summary>
    public string SizeText { get; }
}
