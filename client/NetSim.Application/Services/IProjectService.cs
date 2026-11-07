using System;                        // EventHandler
using System.Collections.Generic;    // IReadOnlyList<T>
using System.Threading.Tasks;        // Task - "a job that will finish later"
using NetSim.Application.Common;     // OperationResult - success / failure answers
using NetSim.Application.Projects;   // Project
using NetSim.Core.Common;            // EntityId

namespace NetSim.Application.Services;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Everything a screen may ask about projects. The screens know only this interface - never the
// class that does the work (ProjectService) and never the storage. An interface = a list of
// promises with no code: "whoever implements me has these members"
public interface IProjectService
{
    // The open project, or null if none is open
    Project? CurrentProject { get; }

    // true when the open project has changes that were not saved yet
    bool IsCurrentProjectDirty { get; }

    // Raised when the open project changes (created / opened / closed)
    event EventHandler? CurrentProjectChanged;

    // Raised when IsCurrentProjectDirty changes
    event EventHandler? DirtyStateChanged;

    // Purpose: Returns all of the user's projects.
    // Input:   nothing.
    // Output:  a Task with the projects, the most recently changed first.
    Task<IReadOnlyList<Project>> GetAllProjectsAsync();

    // Purpose: Creates a new project, stores it, and makes it the open project.
    // Input:   name - the project name; description - optional, may be null.
    // Output:  a Task with Success + the project, or Failure: ValidationError (empty / too long) or Conflict (name taken).
    Task<OperationResult<Project>> CreateProjectAsync(string name, string? description);

    // Purpose: Opens a project and records when it was opened.
    // Input:   projectId - the project's id.
    // Output:  a Task with Success + the project, or Failure NotFound.
    Task<OperationResult<Project>> OpenProjectAsync(EntityId projectId);

    // Purpose: Saves the open project and clears "unsaved changes".
    // Input:   nothing.
    // Output:  a Task with Success, or Failure InvalidState if no project is open.
    Task<OperationResult> SaveCurrentProjectAsync();

    // Purpose: Saves a copy of the open project under a new name, as a new project (new id), and opens the copy.
    // Input:   newName - the name for the copy.
    // Output:  a Task with Success + the copy, or Failure (InvalidState / ValidationError / Conflict).
    Task<OperationResult<Project>> SaveCurrentProjectAsAsync(string newName);

    // Purpose: Renames a project (its id does not change) and stores it right away.
    // Input:   projectId - which project; newName - the new name.
    // Output:  a Task with Success, or Failure (NotFound / ValidationError / Conflict).
    Task<OperationResult> RenameProjectAsync(EntityId projectId, string newName);

    // Purpose: Deletes a project; if it was the open one, nothing is open afterwards.
    // Input:   projectId - which project.
    // Output:  a Task with Success, or Failure NotFound.
    Task<OperationResult> DeleteProjectAsync(EntityId projectId);

    // Purpose: Closes the open project. Does NOT ask about unsaved changes - the screen asks first.
    // Input:   nothing.
    // Output:  nothing.
    void CloseCurrentProject();

    // Purpose: Changes the open project's description in memory only, and marks it "unsaved".
    // Input:   description - the new text, may be null.
    // Output:  Success, or Failure InvalidState if no project is open.
    OperationResult UpdateCurrentProjectDescription(string? description);
}
