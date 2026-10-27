using System.Windows;

namespace VanillaNotes.App.Services;

/// <summary>
/// The size of the text of notes, where they are written and where they are read, as the
/// settings of the profile say. The rest of the interface keeps its size. A size puts two sizes of
/// the tokens in place of the ones notes are drawn with, so that every note on screen follows.
/// </summary>
public sealed class TextSizeService
{
    /// <summary>The key of the size of the text of a note in the tokens.</summary>
    private const string _noteTextKey = "NoteText";

    /// <summary>The key of the size of the code of a note in the tokens.</summary>
    private const string _noteCodeKey = "NoteCode";

    /// <summary>The sizes of the tokens each setting draws a note with: its text and its code, as in tokens.css of the web version.</summary>
    private static readonly Dictionary<string, (string Text, string Code)> _sizes = new(StringComparer.Ordinal)
    {
        ["small"] = ("TextSm", "TextXs"),
        ["normal"] = ("TextMd", "TextSm"),
        ["large"] = ("TextLg", "TextMd"),
    };

    /// <summary>
    /// Shows the text of notes at a size: small, normal, or large.
    /// </summary>
    public void Apply(string size)
    {
        (string text, string code) = _sizes[size];
        ResourceDictionary resources = Application.Current.Resources;
        resources[_noteTextKey] = resources[text];
        resources[_noteCodeKey] = resources[code];
    }
}
