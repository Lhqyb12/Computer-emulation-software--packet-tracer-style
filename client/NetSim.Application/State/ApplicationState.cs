using System;                        // EventHandler, EventArgs
using NetSim.Application.Projects;   // Project

namespace NetSim.Application.State;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Remembers which project is open and whether it has unsaved changes. It knows no rules - the
// project service uses it to remember. One per sign-in (Scoped), so the next user starts clean
public sealed class ApplicationState : IApplicationState
{
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
