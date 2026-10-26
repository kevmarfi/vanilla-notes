using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Domain;

/// <summary>
/// Where the person was in the workspace of the open profile, so that opening it again comes back
/// to the same note and the same folder.
/// </summary>
public sealed class WorkspaceService
{
    /// <summary>The place of a profile never left with something chosen: the whole list, with no note open.</summary>
    private static readonly WorkspacePlace _nowhere = new(null, null, false);

    private readonly WorkspaceRepository _workspace;

    public WorkspaceService(WorkspaceRepository workspace)
    {
        _workspace = workspace;
    }

    /// <summary>Where the person was when they last left the profile.</summary>
    public WorkspacePlace Get() => _workspace.Find() ?? _nowhere;

    /// <summary>Keeps where the person is, as they leave the profile.</summary>
    public void Remember(WorkspacePlace place) => _workspace.Save(place);
}
