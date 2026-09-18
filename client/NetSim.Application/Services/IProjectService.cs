using System;
using System.Collections.Generic;
using NetSim.Application.Common;
using NetSim.Application.Projects;
using NetSim.Core.Common;

namespace NetSim.Application.Services;

/// <summary>
/// Coordinates project lifecycle operations (create/open/save/rename/delete/close) and owns
/// which project is "current" (via <see cref="State.IApplicationState"/>). This is the only
/// door ViewModels use to reach project persistence - none of them talk to
/// <see cref="Persistence.IProjectRepository"/> directly.
/// </summary>
public interface IProjectService
{
    /// <summary>The project currently open, or null if none is open.</summary>
    Project? CurrentProject { get; }

    /// <summary>Whether the current project has changes that have not been saved yet.</summary>
    bool IsCurrentProjectDirty { get; }

    /// <summary>Raised when <see cref="CurrentProject"/> changes (create/open/close).</summary>
    event EventHandler? CurrentProjectChanged;

    /// <summary>Raised when <see cref="IsCurrentProjectDirty"/> changes.</summary>
    event EventHandler? DirtyStateChanged;

    /// <summary>All saved projects, most recently modified first.</summary>
    IReadOnlyList<Project> GetAllProjects();

    /// <summary>
    /// Creates a new project, persists it, and makes it the current project. Fails with
    /// <see cref="OperationErrorType.ValidationError"/> for a blank/too-long name, or
    /// <see cref="OperationErrorType.Conflict"/> if a project with that name already exists.
    /// </summary>
    OperationResult<Project> CreateProject(string name, string? description);

    /// <summary>
    /// Loads the project with the given id and makes it the current project, recording it as
    /// just-opened. Fails with <see cref="OperationErrorType.NotFound"/> if no such project exists.
    /// </summary>
    OperationResult<Project> OpenProject(EntityId projectId);

    /// <summary>
    /// Persists the current project's metadata and clears the dirty flag. Fails with
    /// <see cref="OperationErrorType.InvalidState"/> if no project is currently open.
    /// </summary>
    OperationResult SaveCurrentProject();

    /// <summary>
    /// Persists the current project's metadata under a new name as a brand-new project (new id,
    /// new creation timestamp), leaving the original project untouched, and makes the copy the
    /// current project. Fails the same way <see cref="CreateProject"/> does for an invalid name,
    /// or with <see cref="OperationErrorType.InvalidState"/> if no project is currently open.
    /// </summary>
    OperationResult<Project> SaveCurrentProjectAs(string newName);

    /// <summary>
    /// Renames the project with the given id (its id is unchanged) and persists the change
    /// immediately. Fails the same way <see cref="CreateProject"/> does for an invalid name, or
    /// with <see cref="OperationErrorType.NotFound"/> if no such project exists.
    /// </summary>
    OperationResult RenameProject(EntityId projectId, string newName);

    /// <summary>
    /// Deletes the project with the given id. If it was the current project, clears current-project
    /// state. Fails with <see cref="OperationErrorType.NotFound"/> if no such project exists.
    /// </summary>
    OperationResult DeleteProject(EntityId projectId);

    /// <summary>Closes the current project (if any), leaving <see cref="CurrentProject"/> null.
    /// Does not check for unsaved changes - callers that need to confirm with the user do so
    /// before calling this.</summary>
    void CloseCurrentProject();

    /// <summary>
    /// Updates the current project's description in memory and marks it dirty, without
    /// persisting. Demonstrates the dirty-tracking mechanism via project metadata, since Phase 7
    /// has no network-topology editing yet to dirty the project instead. Fails with
    /// <see cref="OperationErrorType.InvalidState"/> if no project is currently open.
    /// </summary>
    OperationResult UpdateCurrentProjectDescription(string? description);
}
