using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.Application.Projects;
using NetSim.Application.Services;
using NetSim.UI.Common;
using NetSim.UI.Dialogs;
using NetSim.UI.Navigation;

namespace NetSim.UI.ViewModels;

public enum ProjectSortMode
{
    RecentlyModified,
    Name,
}

/// <summary>
/// The Project Management screen: create/open/rename/delete projects and see which one is
/// currently active. Reaches persistence exclusively through <see cref="IProjectService"/> -
/// never through <see cref="Application.Persistence.IProjectRepository"/> or LiteDB directly
/// (see docs/architecture/project-management.md).
/// </summary>
public partial class ProjectsViewModel : ViewModelBase, INavigationAware
{
    private readonly IProjectService _projectService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    private List<Project> _allProjects = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ProjectSortMode _sortMode = ProjectSortMode.RecentlyModified;

    [ObservableProperty]
    private ProjectListItem? _selectedProject;

    public int SortModeIndex
    {
        get => (int)SortMode;
        set => SortMode = (ProjectSortMode)value;
    }

    public ObservableCollection<ProjectListItem> Projects { get; } = new();

    public bool HasProjects => Projects.Count > 0;

    public bool HasNoProjects => !HasProjects;

    public ProjectsViewModel(IProjectService projectService, IDialogService dialogService, INavigationService navigationService)
    {
        _projectService = projectService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        _projectService.CurrentProjectChanged += (_, _) => ApplyFilterAndSort();

        LoadProjects();
    }

    [RelayCommand]
    private Task RefreshAsync() => ExecuteAsync(() =>
    {
        LoadProjects();
        return Task.CompletedTask;
    });

    [RelayCommand]
    private Task NewProjectAsync() => ExecuteAsync(async () =>
    {
        var result = await _dialogService.ShowNewProjectAsync();
        if (result is null)
        {
            return;
        }

        var creation = _projectService.CreateProject(result.Name, result.Description);
        if (!creation.IsSuccess)
        {
            ErrorMessage = creation.ErrorMessage;
            return;
        }

        LoadProjects();
        _navigationService.NavigateTo<NetworkWorkspaceViewModel>();
    });

    [RelayCommand]
    private Task OpenProjectAsync(ProjectListItem? item) => ExecuteAsync(() =>
    {
        if (item is null)
        {
            return Task.CompletedTask;
        }

        var result = _projectService.OpenProject(item.Id);
        ErrorMessage = result.IsSuccess ? null : result.ErrorMessage;
        LoadProjects();

        if (result.IsSuccess)
        {
            _navigationService.NavigateTo<NetworkWorkspaceViewModel>();
        }

        return Task.CompletedTask;
    });

    [RelayCommand]
    private Task RenameProjectAsync(ProjectListItem? item) => ExecuteAsync(async () =>
    {
        if (item is null)
        {
            return;
        }

        var newName = await _dialogService.ShowRenameProjectAsync(item.Name);
        if (newName is null)
        {
            return;
        }

        var result = _projectService.RenameProject(item.Id, newName);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        LoadProjects();
    });

    [RelayCommand]
    private Task DeleteProjectAsync(ProjectListItem? item) => ExecuteAsync(async () =>
    {
        if (item is null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"Delete '{item.Name}'?",
            "This action cannot be undone.",
            confirmText: "Delete",
            isDestructive: true);

        if (!confirmed)
        {
            return;
        }

        var result = _projectService.DeleteProject(item.Id);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        if (ReferenceEquals(SelectedProject, item))
        {
            SelectedProject = null;
        }

        LoadProjects();
    });

    void INavigationAware.OnNavigatedTo() => LoadProjects();

    void INavigationAware.OnNavigatedFrom()
    {
    }

    partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();

    partial void OnSortModeChanged(ProjectSortMode value) => ApplyFilterAndSort();

    private void LoadProjects()
    {
        _allProjects = _projectService.GetAllProjects().ToList();
        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        var currentId = _projectService.CurrentProject?.Id;

        IEnumerable<Project> filtered = _allProjects;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        filtered = SortMode == ProjectSortMode.Name
            ? filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            : filtered.OrderByDescending(p => p.LastModifiedAtUtc);

        Projects.Clear();
        foreach (var project in filtered)
        {
            Projects.Add(new ProjectListItem(project, currentId is not null && project.Id.Equals(currentId.Value)));
        }

        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(HasNoProjects));
    }
}
