using System;                        // EventHandler
using NetSim.Application.Projects;   // Project
using NetSim.Core.Topology;          // Network - the devices and cables of the open project

namespace NetSim.Application.State;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The one place that knows which project (and which network) is open. Instead of a global variable,
// the DI container gives the same object (one per sign-in) to every service that needs it
public interface IApplicationState
{
    // The network of the open project (its devices and cables), or null when no project is open
    Network? CurrentNetwork { get; }

    // Raised when a different network becomes the open one (or the network is closed)
    event EventHandler? CurrentNetworkChanged;

    // Purpose:       Sets the open network (null = no network).
    // Precondition:  none (ProjectService calls it right BEFORE SetCurrentProject, so whoever
    //                listens to "project changed" already finds the right network).
    // Postcondition: CurrentNetwork is the given network; CurrentNetworkChanged was raised only if
    //                it is a different object than before. "Unsaved changes" is NOT touched.
    void SetCurrentNetwork(Network? network);

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
