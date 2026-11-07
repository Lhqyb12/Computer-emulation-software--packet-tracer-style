using System;                           // Guid, StringComparison
using System.Collections.Generic;       // Dictionary, IReadOnlyList
using System.Linq;                      // OrderByDescending, FirstOrDefault, ToList
using System.Threading.Tasks;           // Task, Task.FromResult
using NetSim.Application.Persistence;   // IProjectRepository
using NetSim.Application.Projects;      // Project
using NetSim.Core.Common;               // EntityId

namespace NetSim.App.Persistence;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

/// <summary>
/// Stage-1 stand-in for <see cref="IProjectRepository"/>: keeps projects in memory for the
/// lifetime of the process. There is no NetSim.Infrastructure/database layer yet - real
/// persistence goes through NetSim.Server once that stage is built, not a local file store.
/// </summary>
// "sealed" = no class can inherit from it. ": IProjectRepository" = it promises to have the five
// methods that the workspace expects from a place that stores projects
public sealed class InMemoryProjectRepository : IProjectRepository
{
    // The projects themselves. A Dictionary finds a value by its key quickly:
    // the key is the project's id (a Guid), the value is the project
    private readonly Dictionary<Guid, Project> _projects = new();

        // Purpose: Returns every stored project.
    // Input:   nothing.
    // Output:  a Task with a read-only list of projects, the most recently changed first.
    // Nothing here has to wait, so the answer is wrapped in a Task that is already finished:
    // Task.FromResult(x) = "a Task whose result is x, right now"
    public Task<IReadOnlyList<Project>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Project>>(
            _projects.Values.OrderByDescending(p => p.LastModifiedAtUtc).ToList());   // all the projects, newest change first, as a list

    // Purpose: Finds one project by its id.
    // Input:   id - the project's EntityId.
    // Output:  a Task with the project, or null if there is none with this id.
    // id.Value = the Guid inside the EntityId wrapper
    public Task<Project?> GetByIdAsync(EntityId id) =>
        Task.FromResult(_projects.GetValueOrDefault(id.Value));   // the project for this key, or null - in a finished Task

    // Purpose: Finds one project by its name, ignoring upper/lower case.
    // Input:   name - the project name.
    // Output:  a Task with the project, or null if no project has this name.
    // OrdinalIgnoreCase = "Lab" and "lab" are the same name
    public Task<Project?> GetByNameAsync(string name) =>
        Task.FromResult(
            _projects.Values.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));   // the first project with this name, or null

    // Purpose: Adds a new project or replaces the stored one with the same id.
    // Input:   project - the project to store.
    // Output:  a Task that is already finished.
    // "[key] = value" adds it if the id is new, or replaces the old one if it exists
    public Task SaveAsync(Project project)
    {
        _projects[project.Id.Value] = project;   // add, or replace the one with this id

        // Task.CompletedTask = a Task with no result that is already finished
        return Task.CompletedTask;
    }

    // Purpose: Removes the project with this id.
    // Input:   id - the project's EntityId.
    // Output:  a Task with true if the project existed and was removed, otherwise false.
    public Task<bool> DeleteAsync(EntityId id) =>
        Task.FromResult(_projects.Remove(id.Value));   // remove it; true if it was there - in a finished Task

}
