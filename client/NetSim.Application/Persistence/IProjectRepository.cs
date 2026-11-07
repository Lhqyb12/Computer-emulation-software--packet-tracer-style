using System.Collections.Generic;    // IReadOnlyList - a list you can read but not change
using System.Threading.Tasks;        // Task - every method may have to wait for the server
using NetSim.Application.Projects;   // Project
using NetSim.Core.Common;            // EntityId

namespace NetSim.Application.Persistence;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Where the projects are stored. Only an interface here: NetSim.App decides the real place
// (in memory, or the server). Every method is asynchronous, because a real store may have to wait
// for the network. A method throws if the store refuses or cannot be reached - the screens already
// run every command inside ViewModelBase.ExecuteAsync, which shows the exception's message
public interface IProjectRepository
{
    // Purpose: Reads all the stored projects.
    // Input:   nothing.
    // Output:  a Task with the projects, the most recently modified first.
    Task<IReadOnlyList<Project>> GetAllAsync();

    // Purpose: Finds one project by its id.
    // Input:   id - the project's id.
    // Output:  a Task with the project, or null if there is none.
    Task<Project?> GetByIdAsync(EntityId id);

    // Purpose: Finds one project by its name (upper/lower case ignored).
    // Input:   name - the project name.
    // Output:  a Task with the project, or null if there is none.
    Task<Project?> GetByNameAsync(string name);

    // Purpose: Creates the project, or overwrites the stored one with the same id.
    // Input:   project - the project to store.
    // Output:  a Task that finishes when it is stored.
    Task SaveAsync(Project project);

    // Purpose: Removes one project.
    // Input:   id - the project's id.
    // Output:  a Task with true if it existed and was removed, false if there was no such project.
    Task<bool> DeleteAsync(EntityId id);
}
