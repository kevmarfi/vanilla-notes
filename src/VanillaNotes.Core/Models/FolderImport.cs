namespace VanillaNotes.Core.Models;

/// <summary>What came of bringing a folder of Markdown files into a profile.</summary>
/// <param name="Notes">How many notes were brought in.</param>
/// <param name="Images">How many images the notes point at were attached to them.</param>
/// <param name="LeftOut">How many files were left out: neither a note nor an image a note points at, or a note too long to keep.</param>
public sealed record FolderImport(int Notes, int Images, int LeftOut);
