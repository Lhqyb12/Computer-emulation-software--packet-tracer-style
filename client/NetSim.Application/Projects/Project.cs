using NetSim.Core.Common;   // EntityId - a project's unique id

namespace NetSim.Application.Projects;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One saved network-simulation project. For now it only holds details (name, description, dates);
// the network itself (devices, connections) will be added in a later stage.
// It lives in the Application layer, not in Core, because "project" is about saving and loading,
// not about networks. It holds data only - the rules (empty / taken names...) are in ProjectService
public sealed class Project
{
    // The version of a project's saved shape. Every project made by this version of the program gets 1.
    // Later, when projects get more fields, this number tells old and new saved projects apart
    public const int CurrentSchemaVersion = 1;

    // Purpose: Builds a project object - a new one, or one loaded from storage.
    // Input:   id, name, description (may be null), createdAtUtc, lastModifiedAtUtc,
    //          lastOpenedAtUtc (null = never opened), schemaVersion.
    // Output:  none (a constructor).
    public Project(
        EntityId id,
        string name,
        string? description,
        DateTime createdAtUtc,
        DateTime lastModifiedAtUtc,
        DateTime? lastOpenedAtUtc,
        int schemaVersion)
    {
        Id = id;                                // the unique id
        Name = name;                            // the name
        Description = description;              // the description, may be null
        CreatedAtUtc = createdAtUtc;            // when it was created (UTC)
        LastModifiedAtUtc = lastModifiedAtUtc;  // when it was last changed (UTC)
        LastOpenedAtUtc = lastOpenedAtUtc;      // when it was last opened (UTC), may be null
        SchemaVersion = schemaVersion;          // the version of its saved shape
    }

    // The unique id. Read-only: a project's identity never changes
    public EntityId Id { get; }

    // The name. "private set" = only this class can change it, through Rename()
    public string Name { get; private set; }

    // The description, or null. Changes only through UpdateDescription()
    public string? Description { get; private set; }

    // When it was created (UTC). Read-only: set once
    public DateTime CreatedAtUtc { get; }

    // When it was last changed (UTC). Changes only through Touch(). The list is sorted by it
    public DateTime LastModifiedAtUtc { get; private set; }

    // When it was last opened (UTC), or null if never. Changes only through MarkOpened()
    public DateTime? LastOpenedAtUtc { get; private set; }

    // The version of its saved shape. Read-only
    public int SchemaVersion { get; }

    // Mutations are controlled methods rather than public setters, and take the "what time is
    // it" decision from the caller (ProjectService) instead of reading DateTime.UtcNow
    // internally, so the timestamp update and the reason for it are always decided together at
    // the one place (the service) that already knows why the project changed.

    // Purpose: Changes the name.
    // Input:   name - the new name (already checked by ProjectService - not checked again here).
    // Output:  nothing.
    public void Rename(string name) => Name = name;

    // Purpose: Changes the description.
    // Input:   description - the new text, or null to remove it.
    // Output:  nothing.
    public void UpdateDescription(string? description) => Description = description;

    // Purpose: Records that the project changed.
    // Input:   modifiedAtUtc - when it changed (UTC), decided by the caller.
    // Output:  nothing.
    public void Touch(DateTime modifiedAtUtc) => LastModifiedAtUtc = modifiedAtUtc;

    // Purpose: Records that the project was opened.
    // Input:   openedAtUtc - when it was opened (UTC), decided by the caller.
    // Output:  nothing.
    public void MarkOpened(DateTime openedAtUtc) => LastOpenedAtUtc = openedAtUtc;
}
