using System;
using NetSim.Application.Projects;

namespace NetSim.Application.State;

public sealed class ApplicationState : IApplicationState
{
    public Project? CurrentProject { get; private set; }

    public event EventHandler? CurrentProjectChanged;

    public bool IsCurrentProjectDirty { get; private set; }

    public event EventHandler? IsCurrentProjectDirtyChanged;

    public void SetCurrentProject(Project? project)
    {
        if (!ReferenceEquals(CurrentProject, project))
        {
            CurrentProject = project;
            CurrentProjectChanged?.Invoke(this, EventArgs.Empty);
        }

        SetDirty(false);
    }

    public void MarkCurrentProjectDirty()
    {
        if (CurrentProject is null)
        {
            return;
        }

        SetDirty(true);
    }

    public void MarkCurrentProjectClean() => SetDirty(false);

    private void SetDirty(bool isDirty)
    {
        if (IsCurrentProjectDirty == isDirty)
        {
            return;
        }

        IsCurrentProjectDirty = isDirty;
        IsCurrentProjectDirtyChanged?.Invoke(this, EventArgs.Empty);
    }
}
