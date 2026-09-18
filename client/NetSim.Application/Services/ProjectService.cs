using System;
using System.Collections.Generic;
using NetSim.Application.Common;
using NetSim.Application.Persistence;
using NetSim.Application.Projects;
using NetSim.Application.State;
using NetSim.Core.Common;

namespace NetSim.Application.Services;

public sealed class ProjectService : IProjectService
{
    // "Excessively long" per the Phase 7 spec - generous enough for a real project name,
    // small enough to keep the project list/status bar readable.
    private const int MaxNameLength = 100;

    private readonly IApplicationState _applicationState;
    private readonly IProjectRepository _repository;
    private readonly INetworkService _networkService;

    public ProjectService(IApplicationState applicationState, IProjectRepository repository, INetworkService networkService)
    {
        _applicationState = applicationState;
        _repository = repository;
        _networkService = networkService;

        // Forwarded rather than exposed directly so UI code depends only on IProjectService for
        // project concerns, the same way it already depends on INetworkService/IDeviceService
        // rather than IApplicationState (see docs/architecture/application-layer.md).
        _applicationState.CurrentProjectChanged += (s, e) => CurrentProjectChanged?.Invoke(s, e);
        _applicationState.IsCurrentProjectDirtyChanged += (s, e) => DirtyStateChanged?.Invoke(s, e);
    }

    public Project? CurrentProject => _applicationState.CurrentProject;

    public bool IsCurrentProjectDirty => _applicationState.IsCurrentProjectDirty;

    public event EventHandler? CurrentProjectChanged;

    public event EventHandler? DirtyStateChanged;

    public IReadOnlyList<Project> GetAllProjects() => _repository.GetAll();

    public OperationResult<Project> CreateProject(string name, string? description)
    {
        var validationError = ValidateName(name, excludeId: null);
        if (validationError is not null)
        {
            return OperationResult<Project>.Failure(validationError.Value.ErrorType, validationError.Value.ErrorMessage);
        }

        var now = DateTime.UtcNow;
        var project = new Project(EntityId.New(), name.Trim(), NormalizeDescription(description), now, now, null, Project.CurrentSchemaVersion);

        _repository.Save(project);
        _applicationState.SetCurrentProject(project);
        _networkService.CreateNetwork(project.Name);

        return OperationResult<Project>.Success(project);
    }

    public OperationResult<Project> OpenProject(EntityId projectId)
    {
        var project = _repository.GetById(projectId);
        if (project is null)
        {
            return OperationResult<Project>.Failure(OperationErrorType.NotFound, "Project not found.");
        }

        project.MarkOpened(DateTime.UtcNow);
        _repository.Save(project);
        _applicationState.SetCurrentProject(project);

        // Project metadata does not persist its topology yet (see docs/architecture/device-model.md,
        // "What was deliberately not added") - every open starts from a fresh, empty network so the
        // Device Library (Phase 11) always has somewhere to add devices to, exactly like the canvas
        // itself always starts fresh on project change.
        _networkService.CreateNetwork(project.Name);

        return OperationResult<Project>.Success(project);
    }

    public OperationResult SaveCurrentProject()
    {
        var project = CurrentProject;
        if (project is null)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No project is currently open.");
        }

        project.Touch(DateTime.UtcNow);
        _repository.Save(project);
        _applicationState.MarkCurrentProjectClean();

        return OperationResult.Success();
    }

    public OperationResult<Project> SaveCurrentProjectAs(string newName)
    {
        var current = CurrentProject;
        if (current is null)
        {
            return OperationResult<Project>.Failure(OperationErrorType.InvalidState, "No project is currently open.");
        }

        var validationError = ValidateName(newName, excludeId: null);
        if (validationError is not null)
        {
            return OperationResult<Project>.Failure(validationError.Value.ErrorType, validationError.Value.ErrorMessage);
        }

        var now = DateTime.UtcNow;
        // The original project is never touched here - only a brand-new Project (fresh id,
        // fresh creation timestamp) is built and saved, so "Save As" cannot accidentally
        // overwrite it.
        var copy = new Project(EntityId.New(), newName.Trim(), current.Description, now, now, null, Project.CurrentSchemaVersion);

        _repository.Save(copy);
        _applicationState.SetCurrentProject(copy);

        return OperationResult<Project>.Success(copy);
    }

    public OperationResult RenameProject(EntityId projectId, string newName)
    {
        // If the id matches the current project, mutate that same instance - a freshly loaded
        // instance from the repository would have the same id but not be the object the rest of
        // the UI (and IApplicationState.CurrentProject) is already holding onto.
        var isCurrentProject = CurrentProject is not null && CurrentProject.Id.Equals(projectId);
        var project = isCurrentProject ? CurrentProject : _repository.GetById(projectId);

        if (project is null)
        {
            return OperationResult.Failure(OperationErrorType.NotFound, "Project not found.");
        }

        var validationError = ValidateName(newName, excludeId: projectId);
        if (validationError is not null)
        {
            return OperationResult.Failure(validationError.Value.ErrorType, validationError.Value.ErrorMessage);
        }

        project.Rename(newName.Trim());
        project.Touch(DateTime.UtcNow);
        _repository.Save(project);

        return OperationResult.Success();
    }

    public OperationResult DeleteProject(EntityId projectId)
    {
        var deleted = _repository.Delete(projectId);
        if (!deleted)
        {
            return OperationResult.Failure(OperationErrorType.NotFound, "Project not found.");
        }

        if (CurrentProject is not null && CurrentProject.Id.Equals(projectId))
        {
            _applicationState.SetCurrentProject(null);
            _networkService.ClearNetwork();
        }

        return OperationResult.Success();
    }

    public void CloseCurrentProject()
    {
        _applicationState.SetCurrentProject(null);
        _networkService.ClearNetwork();
    }

    public OperationResult UpdateCurrentProjectDescription(string? description)
    {
        var project = CurrentProject;
        if (project is null)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No project is currently open.");
        }

        project.UpdateDescription(NormalizeDescription(description));
        _applicationState.MarkCurrentProjectDirty();

        return OperationResult.Success();
    }

    private (OperationErrorType ErrorType, string ErrorMessage)? ValidateName(string name, EntityId? excludeId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (OperationErrorType.ValidationError, "Project name cannot be empty.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            return (OperationErrorType.ValidationError, $"Project name cannot exceed {MaxNameLength} characters.");
        }

        var existing = _repository.GetByName(trimmed);
        if (existing is not null && (excludeId is null || !existing.Id.Equals(excludeId.Value)))
        {
            return (OperationErrorType.Conflict, $"A project named '{trimmed}' already exists.");
        }

        return null;
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
