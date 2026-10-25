namespace VanillaNotes.Core.Models;

/// <summary>A note found by a search, with the fragment of its text around the words that matched.</summary>
/// <param name="Note">The note found.</param>
/// <param name="Snippet">A few words of its text around the match, as plain text, with an ellipsis where it was cut.</param>
public sealed record SearchResult(NoteSummary Note, string Snippet);
