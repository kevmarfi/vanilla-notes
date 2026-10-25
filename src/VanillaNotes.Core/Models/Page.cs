namespace VanillaNotes.Core.Models;

/// <summary>One page of a list, and how many there are in all, so that the interface knows whether there is more.</summary>
/// <param name="Items">What the page holds.</param>
/// <param name="Total">How many there are in all the pages.</param>
public sealed record Page<T>(IReadOnlyList<T> Items, int Total);
