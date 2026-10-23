using System;                           // Guid, StringComparison
using System.Collections.Generic;       // Dictionary, IReadOnlyList
using System.Linq;                      // OrderByDescending, FirstOrDefault, ToList
using NetSim.Application.Persistence;   // IProjectRepository
using NetSim.Application.Projects;      // Project
using NetSim.Core.Common;               // EntityId

namespace NetSim.App.Persistence;

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
    // Output:  a read-only list of projects, the most recently changed first.
    // Returns all the projects, the most recently changed first.
    // "=>" = a one-line method that returns this value
    public IReadOnlyList<Project> GetAll() =>
        _projects.Values.OrderByDescending(p => p.LastModifiedAtUtc).ToList();

    // Purpose: Finds one project by its id.
    // Input:   id - the project's EntityId.
    // Output:  the project, or null if there is none with this id.
    // Returns the project with this id, or null if there is none.
    // id.Value = the Guid inside the EntityId wrapper
    public Project? GetById(EntityId id) => _projects.GetValueOrDefault(id.Value);

    // Purpose: Finds one project by its name, ignoring upper/lower case.
    // Input:   name - the project name.
    // Output:  the project, or null if no project has this name.
    // Returns the first project with this name, or null. OrdinalIgnoreCase = "Lab" and "lab" are the same name
    public Project? GetByName(string name) =>
        _projects.Values.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    // Purpose: Adds a new project or replaces the stored one with the same id.
    // Input:   project - the project to store.
    // Output:  nothing.
    // Stores a project. "[key] = value" adds it if the id is new, or replaces the old one if it exists
    public void Save(Project project) => _projects[project.Id.Value] = project;

    // Purpose: Removes the project with this id.
    // Input:   id - the project's EntityId.
    // Output:  true if the project existed and was removed, otherwise false.
    // Removes a project. Returns true if it was there, false if no project had this id
    public bool Delete(EntityId id) => _projects.Remove(id.Value);
}
