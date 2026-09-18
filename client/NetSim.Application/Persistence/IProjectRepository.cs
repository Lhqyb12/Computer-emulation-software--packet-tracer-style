using NetSim.Application.Projects;
using NetSim.Core.Common;

namespace NetSim.Application.Persistence;

/// <summary>
/// Persists <see cref="Project"/> documents - one per saved workspace, unlike
/// <see cref="IUserPreferencesRepository"/>'s single singleton document. Built on the same Phase
/// 6 infrastructure and following the same shape (an interface here, a LiteDB-backed
/// implementation in NetworkSimulator.Infrastructure - see docs/architecture/persistence.md).
/// </summary>
public interface IProjectRepository
{
    /// <summary>All saved projects, ordered by most recently modified first.</summary>
    IReadOnlyList<Project> GetAll();

    /// <summary>Returns the project with the given id, or null if none exists.</summary>
    Project? GetById(EntityId id);

    /// <summary>Returns the project whose name matches (case-insensitively), or null if none exists.</summary>
    Project? GetByName(string name);

    /// <summary>Creates or overwrites the project with a matching id.</summary>
    void Save(Project project);

    /// <summary>Removes the project with the given id. Returns true if a document was deleted.</summary>
    bool Delete(EntityId id);
}
