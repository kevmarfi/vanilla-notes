namespace VanillaNotes.Core.Models;

/// <summary>One backup: when it was made, how much room it takes, and whether the application made it on its own.</summary>
/// <param name="Name">The name of its folder, which is the moment it was made.</param>
/// <param name="CreatedAt">When it was made.</param>
/// <param name="Size">How many bytes its files take, all together.</param>
/// <param name="IsAutomatic">Whether the application made it on its own, once a day; only those are deleted by it.</param>
public sealed record BackupInfo(string Name, DateTimeOffset CreatedAt, long Size, bool IsAutomatic = false);
