using NetSim.Application.Persistence;
using NetSim.Application.Projects;
using NetSim.Core.Common;

namespace NetSim.Application.Tests.TestSupport;

/// <summary>
/// In-memory <see cref="IProjectRepository"/> for Application-layer tests. NetworkSimulator.
/// Application.Tests does not (and should not) reference NetworkSimulator.Infrastructure/LiteDB -
/// ProjectService is tested against this fake, the same way DeviceServiceTests/NetworkServiceTests
/// are tested against a plain in-memory ApplicationState rather than a real database.
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
