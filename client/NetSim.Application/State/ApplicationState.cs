using System;                        // EventHandler, EventArgs
using NetSim.Application.Projects;   // Project
using NetSim.Core.Topology;          // Network

namespace NetSim.Application.State;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Remembers which project and network are open and whether there are unsaved changes. It knows no
// rules - the services use it to remember. One per sign-in (Scoped), so the next user starts clean
public sealed class ApplicationState : IApplicationState
{
    // The network of the open project (its devices and cables), or null when no project is open.
    // "Network?" = a Network OR null; "private set" = everyone can read it, only this class can change it (SetCurrentNetwork)
    public Network? CurrentNetwork { get; private set; }

    // Raised when a different network becomes the open one (or the network is closed). "?" = may have no listeners
    public event EventHandler? CurrentNetworkChanged;

    // Purpose:       Sets the open network.
    // Precondition:  receives network - the network to open, or null to close it (no network).
    // Postcondition: returns nothing - CurrentNetwork is the given network; CurrentNetworkChanged was raised only if it is
    //                a different object than before. The "unsaved changes" flag is NOT touched.
    public void SetCurrentNetwork(Network? network)
    {
        // ReferenceEquals = "is it the very same object in memory?" - the same network is already open, so nothing
        // changes and no event is raised (the canvas would empty itself for nothing)
        if (ReferenceEquals(CurrentNetwork, network))
        {
            return;   // stop here - nothing more to do
        }

        CurrentNetwork = network;                               // keep the new open network
        // Tell the listeners ("?." = only if someone listens): the canvas, the selection and the view empty / reset themselves
        CurrentNetworkChanged?.Invoke(this, EventArgs.Empty);
    }

    // The open project, or null. Only this class can change it
    public Project? CurrentProject { get; private set; }

    // Raised when the open project changes
    public event EventHandler? CurrentProjectChanged;

    // true when the open project has unsaved changes. Starts false
    public bool IsCurrentProjectDirty { get; private set; }

    // Raised when IsCurrentProjectDirty changes
    public event EventHandler? IsCurrentProjectDirtyChanged;

    // Purpose: Sets the open project (null = close it), and marks it as having no unsaved changes.
    // Input:   project - the project to open, or null.
    // Output:  nothing (raises CurrentProjectChanged if the project really changed).
    public void SetCurrentProject(Project? project)
    {
        // Only if it is a different object than the open one (ReferenceEquals = the very same object in memory)
        if (!ReferenceEquals(CurrentProject, project))
        {
            CurrentProject = project;                               // the new open project
            CurrentProjectChanged?.Invoke(this, EventArgs.Empty);   // tell the listeners ("?." = only if someone listens)
        }

        // A project that was just opened / created / closed has no unsaved changes
        SetDirty(false);
    }

    // Purpose: Marks the open project as having unsaved changes.
    // Input:   nothing.
    // Output:  nothing. Does nothing if no project is open.
    public void MarkCurrentProjectDirty()
    {
        // No open project - nothing to mark
        if (CurrentProject is null)
        {
            return;   // stop here - nothing more to do
        }

        // Mark "has unsaved changes"
        SetDirty(true);
    }

    // Purpose: Marks the open project as saved (no unsaved changes).
    // Input:   nothing.
    // Output:  nothing.
    public void MarkCurrentProjectClean() => SetDirty(false);

    // Purpose: Changes the "unsaved changes" flag, and tells the listeners only if it really changed.
    // Input:   isDirty - the new value.
    // Output:  nothing.
    private void SetDirty(bool isDirty)
    {
        // Already this value - nothing changed, so no event
        if (IsCurrentProjectDirty == isDirty)
        {
            return;   // stop here - nothing more to do
        }

        IsCurrentProjectDirty = isDirty;                                // the new value
        IsCurrentProjectDirtyChanged?.Invoke(this, EventArgs.Empty);    // tell the listeners
    }
}
