using System;                        // EventHandler
using NetSim.Application.Projects;   // Project

namespace NetSim.Application.State;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The one place that knows which project is open. Instead of a global variable, the DI container
// gives the same object (one per sign-in) to every service that needs it
public interface IApplicationState
{
    // The open project, or null if none is open
    Project? CurrentProject { get; }

    // Raised when the open project changes
    event EventHandler? CurrentProjectChanged;

    // Purpose: Sets the open project (null = close), and resets "unsaved changes" to false.
    // Input:   project - the project, or null.
    // Output:  nothing.
    void SetCurrentProject(Project? project);

    // true when the open project has changes that were not saved yet
    bool IsCurrentProjectDirty { get; }

    // Raised when IsCurrentProjectDirty changes
    event EventHandler? IsCurrentProjectDirtyChanged;

    // Purpose: Marks the open project as having unsaved changes.
    // Input:   nothing.
    // Output:  nothing (does nothing if no project is open).
    void MarkCurrentProjectDirty();

    // Purpose: Marks the open project as saved.
    // Input:   nothing.
    // Output:  nothing.
    void MarkCurrentProjectClean();
}
