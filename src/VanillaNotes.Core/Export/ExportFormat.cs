namespace VanillaNotes.Core.Export;

/// <summary>The forms the notes of a profile can be taken out in: three of the four of the web version.</summary>
public enum ExportFormat
{
    /// <summary>A ZIP archive with one Markdown file per note, in the folders of the notes, and the attached files beside them.</summary>
    Markdown,

    /// <summary>A ZIP archive with one web page per note, in the folders of the notes, with its images inside the page.</summary>
    Html,

    /// <summary>A single JSON file with every note and what it carries, meant for other programs and for importing.</summary>
    Data,
}
