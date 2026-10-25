namespace VanillaNotes.Core.Models;

/// <summary>
/// A profile of this machine, as the list of profiles shows it: its name, whether a password
/// guards it, and whether Windows Hello can open it. Nothing in it opens the profile.
/// </summary>
/// <param name="Id">The number of the profile, which is also the name of its folder on disk.</param>
/// <param name="Name">The name shown to choose it.</param>
/// <param name="HasPassword">Whether it asks for a password, which also means its data is encrypted.</param>
/// <param name="HasHello">Whether Windows Hello was turned on for it, to open it when its password is forgotten.</param>
/// <param name="CreatedAt">When it was made.</param>
public sealed record Profile(long Id, string Name, bool HasPassword, bool HasHello, DateTimeOffset CreatedAt);
