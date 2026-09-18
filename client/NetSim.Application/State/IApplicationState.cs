using System;
using NetSim.Application.Projects;
using NetSim.Core.Topology;

namespace NetSim.Application.State;

/// <summary>
/// Owns the single "current project" and "current network" the rest of the application
/// coordinates around. Replaces any need for global/static state: this is registered as a
/// singleton and injected wherever current-project/current-network state is needed.
/// </summary>
/// <remarks>
/// <see cref="CurrentProject"/> and <see cref="CurrentNetwork"/> are tracked independently
/// rather than one being derived from the other - Phase 7 (Project Management) only introduces
/// project metadata, not topology persistence (see docs/architecture/project-management.md), so
/// a <see cref="Project"/> has nothing to derive a <see cref="Network"/> from yet. A later phase
/// that adds topology persistence to <see cref="Project"/> is expected to change
/// <see cref="SetCurrentProject"/> to also set the current network from the loaded project.
/// </remarks>
public interface IApplicationState
{
    Network? CurrentNetwork { get; }

    event EventHandler? CurrentNetworkChanged;

    void SetCurrentNetwork(Network? network);

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
