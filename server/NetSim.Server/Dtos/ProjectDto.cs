namespace NetSim.Server.Dtos;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One project as it travels over the socket, in both directions: the server sends a list of these for
// "get-projects", and the client sends one as the data of "save-project".
// Same fields as ProjectDto in the NetworkSimulator project. Deliberately without OwnerUserId (unlike
// ProjectRecord): who owns a project is decided by the server from the token, so the client never needs
// to see that number - and it can't try to set it either, because the field simply isn't here
public class ProjectDto
{
    // The project's id (the Guid inside the client's EntityId)
    public Guid Id { get; set; }

    // The project name
    public string Name { get; set; } = string.Empty;

    // An optional description. null = none
    public string? Description { get; set; }

    // When it was created / last changed / last opened (UTC). LastOpenedAtUtc null = never opened
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastModifiedAtUtc { get; set; }   // when it was last changed (UTC)
    public DateTime? LastOpenedAtUtc { get; set; }   // when it was last opened (UTC), or null

    // The version of the project's saved shape (currently 1)
    public int SchemaVersion { get; set; }
}
