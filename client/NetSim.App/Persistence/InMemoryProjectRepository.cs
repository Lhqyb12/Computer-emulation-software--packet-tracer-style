using System;
using System.Collections.Generic;
using System.Linq;
using NetSim.Application.Persistence;
using NetSim.Application.Projects;
using NetSim.Core.Common;

namespace NetSim.App.Persistence;

/// <summary>
/// Stage-1 stand-in for <see cref="IProjectRepository"/>: keeps projects in memory for the
/// lifetime of the process. There is no NetSim.Infrastructure/database layer yet - real
/// persistence goes through NetSim.Server once that stage is built, not a local file store.
/// </summary>
public sealed class InMemoryProjectRepository : IProjectRepository
{
    private readonly Dictionary<Guid, Project> _projects = new();

    public IReadOnlyList<Project> GetAll() =>
        _projects.Values.OrderByDescending(p => p.LastModifiedAtUtc).ToList();

    public Project? GetById(EntityId id) => _projects.GetValueOrDefault(id.Value);

    public Project? GetByName(string name) =>
        _projects.Values.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public void Save(Project project) => _projects[project.Id.Value] = project;

    public bool Delete(EntityId id) => _projects.Remove(id.Value);
}
