using CommunityToolkit.Mvvm.ComponentModel;  // ObservableObject, [ObservableProperty]
using NetSim.Application.Projects;           // Project
using NetSim.Core.Common;                    // EntityId

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One row of the project list on the Projects screen. It wraps a Project and adds what only the
// screen needs: "is this the open project?". Project itself can't tell the screen about changes,
// and doesn't know which project is open - so this small class does that
public partial class ProjectListItem : ObservableObject
{
    // Purpose: Builds one row.
    // Input:   project - the project of this row; isCurrent - true if it is the open project.
    // Output:  none (a constructor).
    public ProjectListItem(Project project, bool isCurrent)
    {
        Project = project;       // keep the project
        _isCurrent = isCurrent;  // is it the open one (written to the field - nobody is bound yet)
    }

    // The wrapped project. Read-only
    public Project Project { get; }

    // Shortcuts to the project's fields, so the view can write short bindings
    public EntityId Id => Project.Id;                              // the id (the commands send it to the service)
    public string Name => Project.Name;                            // the name
    public string? Description => Project.Description;             // the description, may be null
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);   // show the description line only if there is one
    public DateTime CreatedAtUtc => Project.CreatedAtUtc;          // when it was created
    public DateTime LastModifiedAtUtc => Project.LastModifiedAtUtc; // when it was last changed

    // true = this row is the open project (green dot + "Active" badge).
    // [ObservableProperty] writes the public "IsCurrent" property
    [ObservableProperty]
    private bool _isCurrent;
}
