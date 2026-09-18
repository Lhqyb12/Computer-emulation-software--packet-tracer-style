using CommunityToolkit.Mvvm.ComponentModel;
using NetSim.Application.Projects;
using NetSim.Core.Common;

namespace NetSim.UI.ViewModels;

/// <summary>
/// Per-row display wrapper around a <see cref="Project"/> for the Project Management list.
/// <see cref="Project"/> itself is not observable (see docs/architecture/project-management.md) -
/// this exists solely so the list can show which project is currently active without every row
/// needing to be re-fetched to answer that one question.
/// </summary>
public partial class ProjectListItem : ObservableObject
{
    public ProjectListItem(Project project, bool isCurrent)
    {
        Project = project;
        _isCurrent = isCurrent;
    }

    public Project Project { get; }

    public EntityId Id => Project.Id;

    public string Name => Project.Name;

    public string? Description => Project.Description;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public DateTime CreatedAtUtc => Project.CreatedAtUtc;

    public DateTime LastModifiedAtUtc => Project.LastModifiedAtUtc;

    [ObservableProperty]
    private bool _isCurrent;
}
