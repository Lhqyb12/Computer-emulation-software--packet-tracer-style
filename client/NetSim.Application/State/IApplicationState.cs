using System;
using NetSim.Application.Projects;

namespace NetSim.Application.State;

/// <summary>
/// Owns the single "current project" the rest of the application coordinates around.
/// Replaces any need for global/static state: this is registered as a singleton and injected
/// wherever current-project state is needed.
/// </summary>
public interface IApplicationState
{
    /// <summary>The project currently open in the application, or null if none is open.</summary>
    Project? CurrentProject { get; }

    event EventHandler? CurrentProjectChanged;

    /// <summary>
    /// Sets the current project. Always resets <see cref="IsCurrentProjectDirty"/> to false -
    /// opening/creating/closing a project starts from a clean (saved) state.
    /// </summary>
    void SetCurrentProject(Project? project);

    /// <summary>Whether the current project has modifications that have not been saved yet.</summary>
    bool IsCurrentProjectDirty { get; }

    event EventHandler? IsCurrentProjectDirtyChanged;

    /// <summary>Marks the current project as having unsaved changes. No-op if there is no current project.</summary>
    void MarkCurrentProjectDirty();

    /// <summary>Marks the current project as saved (no unsaved changes).</summary>
    void MarkCurrentProjectClean();
}
