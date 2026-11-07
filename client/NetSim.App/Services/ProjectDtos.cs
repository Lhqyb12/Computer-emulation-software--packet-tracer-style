using System;                      // Guid, DateTime
using System.Collections.Generic;  // List<T>

namespace NetSim.App.Services;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One project as it travels over the socket - matches ProjectDto on the server.
// The client sends one as the data of "save-project", and gets a list of them for "get-projects".
// There is no owner here, on purpose: the server decides the owner from the token
public class ProjectDto
{
    public Guid Id { get; set; }                               // the project's id (the Guid inside EntityId)
    public string Name { get; set; } = string.Empty;           // the project name
    public string? Description { get; set; }                   // an optional description; null = none
    public DateTime CreatedAtUtc { get; set; }                 // when it was created (UTC)
    public DateTime LastModifiedAtUtc { get; set; }            // when it was last changed (UTC)
    public DateTime? LastOpenedAtUtc { get; set; }             // when it was last opened (UTC); null = never
    public int SchemaVersion { get; set; }                     // the version of the project's saved shape
}

// The answer to "get-projects" - matches ProjectListResponse on the server
public class ProjectListResult
{
    public bool Success { get; set; }                          // true = here is the list
    public string Message { get; set; } = string.Empty;        // the reason, when it failed
    public List<ProjectDto> Projects { get; set; } = new();    // the user's projects; an empty list by default, never null
}

// The answer to "save-project" and "delete-project" - matches ProjectResponse on the server
public class ProjectResult
{
    public bool Success { get; set; }                          // true = the action succeeded
    public string Message { get; set; } = string.Empty;        // the text to show when it failed
    public ProjectDto? Project { get; set; }                   // the project as stored, after a save; null otherwise
}
