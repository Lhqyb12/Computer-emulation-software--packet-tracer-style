using NetSim.Core.Common;

namespace NetSim.Application.Projects;

/// <summary>
/// A saved network simulation workspace. This is the Application-layer counterpart to
/// <see cref="Core.Topology.Network"/> - it lives here rather than in
/// <c>NetSim.Core</c> because "project" is a workspace/persistence concept, not a
/// network-domain one, and Core must stay independent of anything that only exists because of
/// saving/loading (see docs/architecture/application-layer.md, "Application state vs a future
/// Project"). For Phase 7 a project only carries metadata; the topology it will eventually wrap
/// (devices, connections, IP/VLAN/routing configuration) is deliberately not modeled yet - see
/// docs/architecture/project-management.md.
/// </summary>
public sealed class Project
{
    /// <summary>
    /// Schema version for the project's persisted shape, stamped on every project created by
    /// this build. Not a migration framework - just the single field a future migration step
    /// would need to tell old and new shapes apart.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    public Project(
        EntityId id,
        string name,
        string? description,
        DateTime createdAtUtc,
        DateTime lastModifiedAtUtc,
        DateTime? lastOpenedAtUtc,
        int schemaVersion)
    {
        Id = id;
        Name = name;
        Description = description;
        CreatedAtUtc = createdAtUtc;
        LastModifiedAtUtc = lastModifiedAtUtc;
        LastOpenedAtUtc = lastOpenedAtUtc;
        SchemaVersion = schemaVersion;
    }

    public EntityId Id { get; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public DateTime LastModifiedAtUtc { get; private set; }

    public DateTime? LastOpenedAtUtc { get; private set; }

    public int SchemaVersion { get; }

    // Mutations are controlled methods rather than public setters, and take the "what time is
    // it" decision from the caller (ProjectService) instead of reading DateTime.UtcNow
    // internally, so the timestamp update and the reason for it are always decided together at
    // the one place (the service) that already knows why the project changed.

    /// <summary>Name validation (blank/too long/duplicate) is an application-level concern - see
    /// <see cref="Services.IProjectService"/> - not re-checked here.</summary>
    public void Rename(string name) => Name = name;

    public void UpdateDescription(string? description) => Description = description;

    public void Touch(DateTime modifiedAtUtc) => LastModifiedAtUtc = modifiedAtUtc;

    public void MarkOpened(DateTime openedAtUtc) => LastOpenedAtUtc = openedAtUtc;
}
