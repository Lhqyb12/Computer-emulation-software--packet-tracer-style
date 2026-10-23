namespace NetSim.Server.Models;

// One saved simulator project, as a row in the Projects table in Postgres (EF Core maps it by convention:
// class "ProjectRecord" + DbSet "Projects" in AppDbContext -> table "Projects").
// Based on ProjectRecord from the NetworkSimulator project, with one addition: OwnerUserId. There, any
// signed-in user could see and delete every project; here every project belongs to exactly one user,
// and the server only ever lets that user read, change or delete it
public class ProjectRecord
{
    // The project's id. A Guid, not an int like User.Id: the client creates it (EntityId.New()) before the
    // project ever reaches the server, so the database must not generate its own number for it
    public Guid Id { get; set; }

    // Who owns this project = the Id of a row in the Users table (a "foreign key").
    // The server fills it from the verified token, never from what the client sends - see MessageRouter
    public int OwnerUserId { get; set; }

    // The owner itself. A "navigation property": EF Core uses it to understand that OwnerUserId points to
    // a User, and creates the foreign-key constraint in the database. null until EF Core loads it
    public User? Owner { get; set; }

    // The project name. Unique per owner (two different users may both have a project called "Lab 1") -
    // see AppDbContext.OnModelCreating
    public string Name { get; set; } = string.Empty;

    // An optional description. "string?" = may be null; null = no description
    public string? Description { get; set; }

    // When the project was created, last changed, and last opened. All in UTC, like User.CreatedAt.
    // LastOpenedAtUtc is "DateTime?" - null = never opened yet
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastModifiedAtUtc { get; set; }
    public DateTime? LastOpenedAtUtc { get; set; }

    // The version of the project's saved shape (Project.CurrentSchemaVersion on the client, currently 1)
    public int SchemaVersion { get; set; }
}
