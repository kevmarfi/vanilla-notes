namespace VanillaNotes.Core.Models;

/// <summary>
/// Where the window of the application was on the screens of this machine, and how large, when it
/// was last closed, to open it there again. The sizes are the ones of the window when it is not
/// maximized, so that it goes back to them when it stops being maximized.
/// </summary>
/// <param name="Left">The distance of its left edge from the left of the main screen.</param>
/// <param name="Top">The distance of its top edge from the top of the main screen.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
/// <param name="IsMaximized">Whether it filled its screen.</param>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool IsMaximized);
