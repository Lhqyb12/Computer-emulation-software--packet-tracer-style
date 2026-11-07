using System;                          // DateTime, EventHandler
using System.Collections.Generic;      // IReadOnlyList<T>
using System.Threading.Tasks;          // Task - "a job that will finish later"
using NetSim.Application.Common;       // OperationResult, OperationErrorType - success / failure answers
using NetSim.Application.Persistence;  // IProjectRepository - the place projects are stored
using NetSim.Application.Projects;     // Project - one project's data
using NetSim.Application.State;        // IApplicationState - which project is open, and unsaved changes
using NetSim.Core.Common;              // EntityId - a project's unique id

namespace NetSim.Application.Services;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The rules of projects: what may be created, opened, saved, renamed and deleted.
// The screens only talk to this class (through IProjectService), never to the storage.
// It does not know WHERE projects are stored - it only knows IProjectRepository, and the
// DI container decides which repository it gets (today: the one that talks to the server)
public sealed class ProjectService : IProjectService
{
    // The longest project name allowed. A named constant, so the check and the message use the same number
    private const int MaxNameLength = 100;

    private readonly IApplicationState _applicationState;   // remembers the open project
    private readonly IProjectRepository _repository;        // stores the projects

    // Purpose: Builds the service.
    // Input:   applicationState, repository - passed in by the DI container.
    // Output:  none (a constructor).
    public ProjectService(IApplicationState applicationState, IProjectRepository repository)
    {
        _applicationState = applicationState;   // keep the open-project state
        _repository = repository;               // keep the storage

        // Pass the two events of the state on as this service's own events, so the screens only
        // need to know IProjectService. "(s, e)" = who raised the event, and extra information
        _applicationState.CurrentProjectChanged += (s, e) => CurrentProjectChanged?.Invoke(s, e);
        _applicationState.IsCurrentProjectDirtyChanged += (s, e) => DirtyStateChanged?.Invoke(s, e);   // pass "unsaved changes changed" on
    }

    // The open project, or null - read from the state
    public Project? CurrentProject => _applicationState.CurrentProject;

    // true when the open project has changes that were not saved yet
    public bool IsCurrentProjectDirty => _applicationState.IsCurrentProjectDirty;

    // Raised when the open project changes (created / opened / closed)
    public event EventHandler? CurrentProjectChanged;

    // Raised when "has unsaved changes" changes
    public event EventHandler? DirtyStateChanged;

    // Purpose: Returns all of the user's projects.
    // Input:   nothing.
    // Output:  a Task with the projects, the most recently changed first.
    // No "async"/"await" needed: it simply hands back the repository's Task, and the caller waits for it
    public Task<IReadOnlyList<Project>> GetAllProjectsAsync() => _repository.GetAllAsync();

    // Purpose: Creates a new project, stores it, and makes it the open project.
    // Input:   name - the name the user typed; description - optional text, may be null.
    // Output:  a Task with Success + the new project, or Failure (empty / too long / taken name).
    //          Throws if the storage fails (e.g. the server is down) - the screen catches it.
    public async Task<OperationResult<Project>> CreateProjectAsync(string name, string? description)
    {
        // Check the name. excludeId: null = every other project with this name is a clash
        var validationError = await ValidateNameAsync(name, excludeId: null);

        // A problem with the name - return it as a failure. ".Value" = the pair inside the nullable
        if (validationError is not null)
        {
            return OperationResult<Project>.Failure(validationError.Value.ErrorType, validationError.Value.ErrorMessage);
        }

        // The time now, in UTC, taken once so "created" and "modified" are exactly equal
        var now = DateTime.UtcNow;

        // The new project: a new random id, the trimmed name, the cleaned description,
        // created now, modified now, never opened (null), schema version 1
        var project = new Project(EntityId.New(), name.Trim(), NormalizeDescription(description), now, now, null, Project.CurrentSchemaVersion);

        // Store it, and wait. If this throws, the next lines don't run - so nothing unsaved becomes "open"
        await _repository.SaveAsync(project);   // store it, and wait

        // Make it the open project (this raises CurrentProjectChanged)
        _applicationState.SetCurrentProject(project);

        // Success, with the new project
        return OperationResult<Project>.Success(project);
    }

    // Purpose: Opens an existing project and records when it was opened.
    // Input:   projectId - the id of the project to open.
    // Output:  a Task with Success + the project, or Failure "Project not found.".
    public async Task<OperationResult<Project>> OpenProjectAsync(EntityId projectId)

    {
        // Find it in the storage, and wait
        var project = await _repository.GetByIdAsync(projectId);

        // No project with this id
        if (project is null)
        {
            return OperationResult<Project>.Failure(OperationErrorType.NotFound, "Project not found.");
        }

        // Write "opened now" into the project, and store that change
        project.MarkOpened(DateTime.UtcNow);
        await _repository.SaveAsync(project);   // store it, and wait

        // Make it the open project
        _applicationState.SetCurrentProject(project);

        // Success, with the project
        return OperationResult<Project>.Success(project);
    }

    // Purpose: Saves the open project and clears "unsaved changes".
    // Input:   nothing (uses the open project).
    // Output:  a Task with Success, or Failure if no project is open.
    public async Task<OperationResult> SaveCurrentProjectAsync()

    {
        // The open project, kept in a local variable
        var project = CurrentProject;

        // Nothing is open - nothing to save
        if (project is null)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No project is currently open.");
        }

        // Update "last modified" to now, and store it
        project.Touch(DateTime.UtcNow);
        await _repository.SaveAsync(project);   // store it, and wait

        // There are no unsaved changes any more
        _applicationState.MarkCurrentProjectClean();

        // Success, nothing to return
        return OperationResult.Success();
    }

    // Purpose: Saves a copy of the open project under a new name, as a brand-new project.
    // Input:   newName - the name for the copy.
    // Output:  a Task with Success + the copy, or Failure (nothing open / bad or taken name).
     public async Task<OperationResult<Project>> SaveCurrentProjectAsAsync(string newName)

    {
        // The open project
        var current = CurrentProject;

        // Nothing is open - nothing to copy
        if (current is null)
        {
            return OperationResult<Project>.Failure(OperationErrorType.InvalidState, "No project is currently open.");
        }

        // Check the new name (the original's name counts as taken too - the copy is a separate project)
        var validationError = await ValidateNameAsync(newName, excludeId: null);

        // A problem with the name
        if (validationError is not null)
        {
            return OperationResult<Project>.Failure(validationError.Value.ErrorType, validationError.Value.ErrorMessage);
        }

        // The time now, once
        var now = DateTime.UtcNow;
        // The original project is never touched here - only a brand-new Project (fresh id,
        // fresh creation timestamp) is built and saved, so "Save As" cannot accidentally
        // overwrite it.
        var copy = new Project(EntityId.New(), newName.Trim(), current.Description, now, now, null, Project.CurrentSchemaVersion);

        // Store the copy
        await _repository.SaveAsync(copy);

        // The copy becomes the open project
        _applicationState.SetCurrentProject(copy);

        // Success, with the copy
        return OperationResult<Project>.Success(copy);
    }

    // Purpose: Renames a project (its id stays the same) and stores the change right away.
    // Input:   projectId - which project; newName - the new name.
    // Output:  a Task with Success, or Failure (not found / bad or taken name).
    public async Task<OperationResult> RenameProjectAsync(EntityId projectId, string newName)

    {
        // If the id matches the current project, mutate that same instance - a freshly loaded
        // instance from the repository would have the same id but not be the object the rest of
        // the UI (and IApplicationState.CurrentProject) is already holding onto.
        var isCurrentProject = CurrentProject is not null && CurrentProject.Id.Equals(projectId);

        // "a ? b : c" = the open project if it's that one, otherwise wait for the storage to find it
        var project = isCurrentProject ? CurrentProject : await _repository.GetByIdAsync(projectId);

        // No project with this id
        if (project is null)
        {
            return OperationResult.Failure(OperationErrorType.NotFound, "Project not found.");
        }

        // Check the new name. excludeId: projectId = the project itself doesn't count as "taken"
         var validationError = await ValidateNameAsync(newName, excludeId: projectId);

        // A problem with the name
        if (validationError is not null)
        {
            return OperationResult.Failure(validationError.Value.ErrorType, validationError.Value.ErrorMessage);
        }

        project.Rename(newName.Trim());      // change the name (without spaces at both ends)
        project.Touch(DateTime.UtcNow);      // update "last modified"
        await _repository.SaveAsync(project); // store it


        // Success, nothing to return
        return OperationResult.Success();
    }

    // Purpose: Deletes a project; if it was the open one, nothing is open afterwards.
    // Input:   projectId - which project.
    // Output:  a Task with Success, or Failure "Project not found.".
    public async Task<OperationResult> DeleteProjectAsync(EntityId projectId)

    {
        // Ask the storage to delete it. true = it existed and was deleted
        var deleted = await _repository.DeleteAsync(projectId);

        // There was no such project
        if (!deleted)
        {
            return OperationResult.Failure(OperationErrorType.NotFound, "Project not found.");
        }

        // If the deleted project was the open one, close it - otherwise the app would keep a deleted project open
        if (CurrentProject is not null && CurrentProject.Id.Equals(projectId))
        {
            _applicationState.SetCurrentProject(null);
        }

        // Success, nothing to return
        return OperationResult.Success();
    }

    // Purpose: Closes the open project (if any). Does not ask about unsaved changes - the screen asks first.
    // Input:   nothing.
    // Output:  nothing.
    public void CloseCurrentProject()
    {
        // null = no project is open
        _applicationState.SetCurrentProject(null);
    }

    // Purpose: Changes the open project's description in memory only, and marks it "unsaved".
    // Input:   description - the new text, may be null.
    // Output:  Success, or Failure if no project is open.
    public OperationResult UpdateCurrentProjectDescription(string? description)
    {
        // The open project
        var project = CurrentProject;

        // Nothing is open
        if (project is null)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No project is currently open.");
        }

        project.UpdateDescription(NormalizeDescription(description));   // change it in memory
        _applicationState.MarkCurrentProjectDirty();                     // mark "has unsaved changes"

        // Success, nothing to return
        return OperationResult.Success();
    }

    // Purpose: Checks a project name: not empty, not too long, not taken by another project.
    // Input:   name - the name to check; excludeId - a project that may keep this name (itself, on rename), or null.
    // Output:  a Task with null if the name is fine, or a pair (error type, message) if it is not.
    // "(OperationErrorType ErrorType, string ErrorMessage)?" = two values travelling together, or null
    private async Task<(OperationErrorType ErrorType, string ErrorMessage)?> ValidateNameAsync(string name, EntityId? excludeId)

    {
        // Empty, or only spaces
        if (string.IsNullOrWhiteSpace(name))
        {
            return (OperationErrorType.ValidationError, "Project name cannot be empty.");
        }

        // The name without spaces at both ends
        var trimmed = name.Trim();

        // Longer than 100 characters
        if (trimmed.Length > MaxNameLength)
        {
            return (OperationErrorType.ValidationError, $"Project name cannot exceed {MaxNameLength} characters.");
        }

        // Is there already a project with this name (any upper/lower case)? Wait for the storage
        var existing = await _repository.GetByNameAsync(trimmed);

        // Found one, and it is not the excluded project - the name is taken
        if (existing is not null && (excludeId is null || !existing.Id.Equals(excludeId.Value)))
        {
            return (OperationErrorType.Conflict, $"A project named '{trimmed}' already exists.");
        }

        // No problem
        return null;
    }

    // Purpose: Cleans a description before it is stored.
    // Input:   description - what the user typed, may be null.
    // Output:  null if it is empty or only spaces, otherwise the text without spaces at both ends.
    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
