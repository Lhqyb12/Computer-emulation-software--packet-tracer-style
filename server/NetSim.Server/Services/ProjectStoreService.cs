using Microsoft.EntityFrameworkCore;  // AsNoTracking, FirstOrDefaultAsync, AnyAsync, ToListAsync - asynchronous DB queries
using NetSim.Server.Data;             // AppDbContext
using NetSim.Server.Dtos;             // ProjectDto, ProjectResponse
using NetSim.Server.Models;           // ProjectRecord

namespace NetSim.Server.Services;

// Stores the users' simulator projects in the Projects table, safely.
// The database part is based on PostgresProjectRepository / ProjectMapper from the NetworkSimulator project,
// and the name check on ProjectsController.ValidateName there. What is added here is ownership: every method
// gets the caller's user id (ownerId), which MessageRouter reads from the verified token - never from the
// message's data - and a user can only ever see, change or delete their own projects
public class ProjectStoreService
{
    // The same limit the client uses (ProjectService.MaxNameLength) - checked again here, because the server
    // must never trust that the client already checked
    private const int MaxNameLength = 100;

    // The connection to the database
    private readonly AppDbContext _db;

    // Records security events (here: someone trying to touch another user's project)
    private readonly SecurityLog _security;

    // Purpose: Builds the project store.
    // Input:   db - the database; security - the security event log.
    // Output:  none (a constructor) - a new ProjectStoreService.
    // The constructor. Dependency injection passes in the two objects, exactly like UserService
    public ProjectStoreService(AppDbContext db, SecurityLog security)
    {
        _db = db;
        _security = security;
    }

    // Purpose: Reads the projects of one user.
    // Input:   ownerId - the signed-in user's id, from the verified token.
    // Output:  that user's projects as ProjectDto rows, most recently modified first (an empty list if none).
    public async Task<List<ProjectDto>> GetAllAsync(int ownerId)
    {
        return await _db.Projects
            // Read-only query: EF Core doesn't need to watch these rows for changes, which is faster
            .AsNoTracking()
            // Only this user's rows. This one line is what stops a user from ever seeing someone else's projects
            .Where(p => p.OwnerUserId == ownerId)
            // Most recently changed first (uses the LastModifiedAtUtc index from AppDbContext)
            .OrderByDescending(p => p.LastModifiedAtUtc)
            // Turn every row into a ProjectDto - without OwnerUserId, so it never leaves the server
            .Select(p => new ProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                CreatedAtUtc = p.CreatedAtUtc,
                LastModifiedAtUtc = p.LastModifiedAtUtc,
                LastOpenedAtUtc = p.LastOpenedAtUtc,
                SchemaVersion = p.SchemaVersion
            })
            // Run the query and collect the results into a list
            .ToListAsync();
    }

    // Purpose: Creates a new project or updates an existing one - but only if it belongs to the caller.
    // Input:   ownerId and ownerEmail - who is asking, from the verified token; project - the data the client sent.
    // Output:  a ProjectResponse: Success = true with the project as stored, otherwise false with the reason.
    public async Task<ProjectResponse> SaveAsync(int ownerId, string? ownerEmail, ProjectDto? project)
    {
        // Nothing (or no id) was sent - nothing to save
        if (project is null || project.Id == Guid.Empty)
            return Fail("No project was sent.");

        // The same name rules as the client's ProjectService - checked again, because the client can't be trusted
        if (string.IsNullOrWhiteSpace(project.Name))
            return Fail("Project name cannot be empty.");

        var name = project.Name.Trim();
        if (name.Length > MaxNameLength)
            return Fail($"Project name cannot exceed {MaxNameLength} characters.");

        // Is there already a project with this id? null = no, so this save creates a new one
        var existing = await _db.Projects.FirstOrDefaultAsync(p => p.Id == project.Id);

        // There is, but it belongs to someone else: refuse, and record the attempt. The answer is deliberately
        // the same "not found" a missing project gets - so it doesn't even reveal that this id exists
        if (existing is not null && existing.OwnerUserId != ownerId)
        {
            await _security.RecordAsync("Project access denied", ownerEmail,
                $"Tried to overwrite project {project.Id}, which belongs to another user");
            return Fail("Project not found.");
        }

        // Does this user already have a different project with the same name, ignoring upper/lower case?
        // ("Lab 1" and "lab 1" clash.) Only this user's projects are checked - another user may use the same name.
        // ToLower() on both sides instead of the original's EF.Functions.ILike: ILike treats "%" and "_" in the
        // name as wildcards, so a project called "Lab_1" would wrongly clash with "Lab11"
        var lowerName = name.ToLower();
        var nameTaken = await _db.Projects.AnyAsync(p =>
            p.OwnerUserId == ownerId && p.Id != project.Id && p.Name.ToLower() == lowerName);
        if (nameTaken)
            return Fail($"A project named '{name}' already exists.");

        // An empty description is stored as null, like the client's ProjectService does
        var description = string.IsNullOrWhiteSpace(project.Description) ? null : project.Description.Trim();

        // A new project: create the row. OwnerUserId comes from the token (ownerId), never from the client.
        // The creation time is set only here, once - an update below never changes it
        if (existing is null)
        {
            existing = new ProjectRecord
            {
                Id = project.Id,
                OwnerUserId = ownerId,
                CreatedAtUtc = ToUtc(project.CreatedAtUtc)
            };
            _db.Projects.Add(existing);
        }

        // New or existing - copy in the fields the client is allowed to change
        existing.Name = name;
        existing.Description = description;
        existing.LastModifiedAtUtc = ToUtc(project.LastModifiedAtUtc);
        existing.LastOpenedAtUtc = project.LastOpenedAtUtc is null ? null : ToUtc(project.LastOpenedAtUtc.Value);
        existing.SchemaVersion = project.SchemaVersion;

        // Run the INSERT or UPDATE statement
        await _db.SaveChangesAsync();

        // Send back the project as it is now stored
        return new ProjectResponse { Success = true, Project = ToDto(existing) };
    }

    // Purpose: Deletes one project - but only if it belongs to the caller.
    // Input:   ownerId and ownerEmail - who is asking, from the verified token; projectId - the project to delete.
    // Output:  a ProjectResponse: Success = true if it was deleted, otherwise false with the reason.
    public async Task<ProjectResponse> DeleteAsync(int ownerId, string? ownerEmail, Guid projectId)
    {
        // Find the project. null = no project has this id
        var existing = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (existing is null)
            return Fail("Project not found.");

        // It exists, but belongs to someone else: refuse with the same "not found", and record the attempt
        if (existing.OwnerUserId != ownerId)
        {
            await _security.RecordAsync("Project access denied", ownerEmail,
                $"Tried to delete project {projectId}, which belongs to another user");
            return Fail("Project not found.");
        }

        // Mark the row for deletion, and run the DELETE statement
        _db.Projects.Remove(existing);
        await _db.SaveChangesAsync();

        return new ProjectResponse { Success = true };
    }

    // Purpose: Turns a database row into the shape that travels over the socket.
    // Input:   record - a row of the Projects table.
    // Output:  a ProjectDto with the same fields, except OwnerUserId. (Based on ProjectMapping.ToDto.)
    private static ProjectDto ToDto(ProjectRecord record) => new()
    {
        Id = record.Id,
        Name = record.Name,
        Description = record.Description,
        CreatedAtUtc = record.CreatedAtUtc,
        LastModifiedAtUtc = record.LastModifiedAtUtc,
        LastOpenedAtUtc = record.LastOpenedAtUtc,
        SchemaVersion = record.SchemaVersion
    };

    // Purpose: Makes sure a time is marked as UTC before it goes into a "timestamp with time zone" column.
    // Input:   value - a time from the client's message.
    // Output:  the same moment, with Kind = Utc. (Based on ProjectMapper.ToUtc.)
    // Npgsql refuses to store a DateTime whose Kind is not Utc in such a column, so this is checked once, here
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    // Purpose: Builds a "refused" answer.
    // Input:   message - why it was refused.
    // Output:  a ProjectResponse with Success = false and that message.
    private static ProjectResponse Fail(string message) => new() { Success = false, Message = message };
}
