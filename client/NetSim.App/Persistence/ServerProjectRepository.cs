using System;                           // Guid, StringComparison, InvalidOperationException
using System.Collections.Generic;       // IReadOnlyList
using System.Linq;                      // Select, FirstOrDefault, ToList
using System.Threading.Tasks;           // Task
using NetSim.App.Services;              // AuthApiClient, ProjectDto
using NetSim.Application.Persistence;   // IProjectRepository
using NetSim.Application.Projects;      // Project
using NetSim.Application.State;         // ICurrentUser - holds the signed-in user's token
using NetSim.Core.Common;               // EntityId

namespace NetSim.App.Persistence;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The real IProjectRepository: every method asks the server, over the encrypted socket, through
// AuthApiClient. It replaces InMemoryProjectRepository - ProjectService does not know the difference,
// because it only knows the interface.
// When the server refuses or cannot be reached, the method throws with the server's message;
// ViewModelBase.ExecuteAsync on the screen catches it and shows the message to the user
public sealed class ServerProjectRepository : IProjectRepository
{
    // Our connection to the server - the same way the login screen creates its own
    private readonly AuthApiClient _api = new();

    // Who is signed in. Its Token goes with every request, so the server knows whose projects these are
    private readonly ICurrentUser _currentUser;

    // Purpose: Builds the repository. Dependency injection passes in the signed-in user.
    // Input:   currentUser - the user of this sign-in (filled by MainWindowViewModel).
    // Output:  none (a constructor).
    public ServerProjectRepository(ICurrentUser currentUser)
    {
        _currentUser = currentUser;   // keep the signed-in user, to read its token later
    }

    // Purpose: Reads the signed-in user's projects from the server.
    // Input:   nothing.
    // Output:  a Task with the projects, the most recently changed first (the server already sorts them).
    public async Task<IReadOnlyList<Project>> GetAllAsync()
    {
        // Ask the server for the list, with the token, and wait for the answer
        var result = await _api.GetProjectsAsync(_currentUser.Token);

        // Refused (bad token) or the server is down - let the screen show why
        if (!result.Success)
            throw new InvalidOperationException(result.Message);

        // Every ProjectDto from the server becomes a Project of the Application layer
        return result.Projects.Select(ToProject).ToList();
    }

    // Purpose: Finds one of the user's projects by its id.
    // Input:   id - the project's EntityId.
    // Output:  a Task with the project, or null if there is none with this id.
    // The server has no "get one project" message, so read the whole list and look in it -
    // a user has only a few projects, so this is fast enough
    public async Task<Project?> GetByIdAsync(EntityId id)
    {
        var all = await GetAllAsync();                       // the whole list from the server
        return all.FirstOrDefault(p => p.Id.Equals(id));     // the first with this id, or null
    }

    // Purpose: Finds one of the user's projects by its name, ignoring upper/lower case.
    // Input:   name - the project name.
    // Output:  a Task with the project, or null if no project has this name.
    public async Task<Project?> GetByNameAsync(string name)
    {
        var all = await GetAllAsync();                       // the whole list from the server

        // the first with this name ("Lab" and "lab" are the same name), or null
        return all.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    // Purpose: Creates or updates one project on the server.
    // Input:   project - the project to store.
    // Output:  a Task. Throws with the server's message if the save was refused.
    public async Task SaveAsync(Project project)
    {
        // Turn the project into the server's shape, send it with the token, and wait for the answer
        var result = await _api.SaveProjectAsync(ToDto(project), _currentUser.Token);

        // Refused (e.g. the name is taken) or the server is down - throw, with the server's message
        if (!result.Success)
            throw new InvalidOperationException(result.Message);
    }

    // Purpose: Deletes one project on the server.
    // Input:   id - the project's EntityId.
    // Output:  a Task with true if it was deleted, false if the server has no such project of this user.
    public async Task<bool> DeleteAsync(EntityId id)
    {
        // Send the Guid inside the id, with the token, and wait for the answer
        var result = await _api.DeleteProjectAsync(id.Value, _currentUser.Token);

        // Deleted
        if (result.Success)
            return true;

        // "Not found" is a normal answer (ProjectService turns false into its own "Project not found."
        // message). Anything else - no token, no connection - is a real problem, so it is thrown
        if (result.Message == "Project not found.")
            return false;

        // Any other failure - throw, with the server's message
        throw new InvalidOperationException(result.Message);
    }

    // Purpose: Turns a project from the server into a Project of the Application layer.
    // Input:   dto - one project as it came over the socket.
    // Output:  a Project with the same id and fields.
    // EntityId.FromValue = rebuild the id the project already has, instead of making a new one
    private static Project ToProject(ProjectDto dto) => new(
        EntityId.FromValue(dto.Id),   // the existing id
        dto.Name,                      // the name
        dto.Description,               // the description (may be null)
        dto.CreatedAtUtc,              // when it was created
        dto.LastModifiedAtUtc,         // when it was last changed
        dto.LastOpenedAtUtc,           // when it was last opened (may be null)
        dto.SchemaVersion);            // the version of its saved shape

    // Purpose: Turns a Project into the shape the server expects.
    // Input:   project - a Project of the Application layer.
    // Output:  a ProjectDto with the same fields (no owner - the server takes it from the token).
    private static ProjectDto ToDto(Project project) => new()
    {
        Id = project.Id.Value,                         // the Guid inside the EntityId
        Name = project.Name,                           // the name
        Description = project.Description,             // the description (may be null)
        CreatedAtUtc = project.CreatedAtUtc,           // when it was created
        LastModifiedAtUtc = project.LastModifiedAtUtc, // when it was last changed
        LastOpenedAtUtc = project.LastOpenedAtUtc,     // when it was last opened (may be null)
        SchemaVersion = project.SchemaVersion          // the version of its saved shape
    };
}
