using System;                                // StringComparison, StringComparer
using System.Collections.Generic;            // List<T>, IEnumerable<T>
using System.Collections.ObjectModel;        // ObservableCollection<T> - a list that tells the screen when items are added/removed
using System.Linq;                           // Where, OrderBy, OrderByDescending, ToList - ready-made list operations
using System.Threading.Tasks;                // Task - "a job that will finish later", used to wait without freezing the screen
using CommunityToolkit.Mvvm.ComponentModel;  // [ObservableProperty] - writes the public property for a private field
using CommunityToolkit.Mvvm.Input;           // [RelayCommand] - turns a method into a Command a button can call
using NetSim.Application.Projects;           // Project - one project's data
using NetSim.Application.Services;           // IProjectService - the only door this screen uses to reach the projects
using NetSim.UI.Common;                      // ViewModelBase - the base class of every screen (IsBusy, ErrorMessage, ExecuteAsync)
using NetSim.UI.Dialogs;                     // IDialogService - opens the New / Rename / Confirm pop-up windows
using NetSim.UI.Navigation;                  // INavigationService, INavigationAware - moving between screens

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The two ways the list can be sorted. An enum = a closed list of named values
public enum ProjectSortMode
{
    RecentlyModified,   // 0 - the project changed last comes first
    Name,               // 1 - A to Z by name
}

// The "Projects" screen: create, open, rename and delete projects, and see which one is open.
// It never touches the storage itself - it only asks IProjectService
public partial class ProjectsViewModel : ViewModelBase, INavigationAware
{
    private readonly IProjectService _projectService;        // the project rules and storage
    private readonly IDialogService _dialogService;          // the pop-up windows
    private readonly INavigationService _navigationService;  // moving between screens (kept for later use)

    // Every project the service returned, before search and sort. The search filters from this
    // list, so typing a letter does not ask the server again
    private List<Project> _allProjects = new();

    // The text in the search box. [ObservableProperty] writes a public "SearchText" property for it,
    // which tells the screen whenever it changes
    [ObservableProperty]
    private string _searchText = string.Empty;

    // The chosen sort. Generates a public "SortMode" property
    [ObservableProperty]
    private ProjectSortMode _sortMode = ProjectSortMode.RecentlyModified;

    // The row the user selected in the list, or null. Generates a public "SelectedProject" property
    [ObservableProperty]
    private ProjectListItem? _selectedProject;

    // The sort as a number, for the drop-down list on the screen (it works with item numbers 0, 1).
    // "(int)" turns the enum into its number; "(ProjectSortMode)" turns the number back into the enum
    public int SortModeIndex
    {
        get => (int)SortMode;                       // reading: enum -> number
        set => SortMode = (ProjectSortMode)value;   // writing: number -> enum
    }

    // The rows shown on the screen. ObservableCollection tells the screen about every Add/Clear,
    // so the list on the screen updates by itself
    public ObservableCollection<ProjectListItem> Projects { get; } = new();

    // true when there is at least one row - the screen shows the list
    public bool HasProjects => Projects.Count > 0;

    // the opposite - the screen shows the "No projects yet" box
    public bool HasNoProjects => !HasProjects;

    // Purpose: Builds the screen's view model.
    // Input:   projectService, dialogService, navigationService - passed in by the DI container.
    // Output:  none (a constructor).
    // The list is NOT loaded here: a constructor can't wait. The navigation service calls OnNavigatedTo
    // right after building this screen, and that loads the list
    public ProjectsViewModel(IProjectService projectService, IDialogService dialogService, INavigationService navigationService)
    {
        _projectService = projectService;          // keep the project service
        _dialogService = dialogService;            // keep the dialog service
        _navigationService = navigationService;    // keep the navigation service

        // When the open project changes (created/opened/closed), rebuild the rows so the
        // "Active" mark moves to the right row. "(_, _)" = the event's two inputs, not used here
        _projectService.CurrentProjectChanged += (_, _) => ApplyFilterAndSort();

    }

    // Purpose: The "Refresh" command - reads the list again.
    // Input:   nothing.
    // Output:  a Task that finishes when the list is loaded.
    // [RelayCommand] creates "RefreshCommand" from this method (without the word Async).
    // ExecuteAsync (in ViewModelBase) runs the work safely: shows "busy", clears the old error,
    // and turns any exception into ErrorMessage instead of crashing
    [RelayCommand]
    private Task RefreshAsync() => ExecuteAsync(() => LoadProjectsAsync());


    // Purpose: The "New Project" command - asks for a name, creates the project, reloads the list.
    // Input:   nothing.
    // Output:  a Task that finishes when the work is done (or the user cancelled).
    // "async () =>" = a small nameless function that is allowed to wait ("await") inside it
    [RelayCommand]
    private Task NewProjectAsync() => ExecuteAsync(async () =>
    {
        // Open the "New Project" window and wait until the user presses Create or Cancel
        var result = await _dialogService.ShowNewProjectAsync();

        // null = the user pressed Cancel - nothing to do
        if (result is null)
        {
            return;   // stop here - nothing more to do
        }

        // Ask the service to create the project, and wait (it may go to the server)
        var creation = await _projectService.CreateProjectAsync(result.Name, result.Description);

        // An expected failure (e.g. the name is taken) comes back as a result, not an exception
        if (!creation.IsSuccess)
        {
            ErrorMessage = creation.ErrorMessage;   // show it in the red box
            return;   // stop here - nothing more to do
        }

        // It worked - read the list again so the new project appears
        await LoadProjectsAsync();

    });

    // Purpose: The "Open" button of one row - makes that project the open one.
    // Input:   item - the row whose button was clicked (passed by the button).
    // Output:  a Task that finishes when the project is open and the list reloaded.
    [RelayCommand]
     private Task OpenProjectAsync(ProjectListItem? item) => ExecuteAsync(async () =>
    {
        // No row was passed - nothing to open
        if (item is null)
        {
            return;   // stop here - nothing more to do
        }

        // Ask the service to open it, and wait
        var result = await _projectService.OpenProjectAsync(item.Id);

        // "a ? b : c" = if a then b, otherwise c: no error on success, the message on failure
        ErrorMessage = result.IsSuccess ? null : result.ErrorMessage;

        // Read the list again so the "Active" mark moves to this row
        await LoadProjectsAsync();
    });


    // Purpose: The "Rename" button of one row - asks for a new name and renames the project.
    // Input:   item - the row whose button was clicked.
    // Output:  a Task that finishes when the work is done (or the user cancelled).
    [RelayCommand]
    private Task RenameProjectAsync(ProjectListItem? item) => ExecuteAsync(async () =>
    {
        // No row was passed - nothing to rename
        if (item is null)
        {
            return;   // stop here - nothing more to do
        }

        // Open the "Rename" window with the current name filled in, and wait
        var newName = await _dialogService.ShowRenameProjectAsync(item.Name);

        // null = Cancel
        if (newName is null)
        {
            return;   // stop here - nothing more to do
        }

        // Ask the service to rename it, and wait
        var result = await _projectService.RenameProjectAsync(item.Id, newName);

        // e.g. the new name is taken - show why
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;   // stop here - nothing more to do
        }

        // Read the list again so the new name appears
        await LoadProjectsAsync();

    });

    // Purpose: The delete button of one row - asks "are you sure?" and deletes the project.
    // Input:   item - the row whose button was clicked.
    // Output:  a Task that finishes when the work is done (or the user cancelled).
    [RelayCommand]
    private Task DeleteProjectAsync(ProjectListItem? item) => ExecuteAsync(async () =>
    {
        // No row was passed - nothing to delete
        if (item is null)
        {
            return;   // stop here - nothing more to do
        }

        // Ask before deleting, because it can't be undone. isDestructive = the button is red
        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"Delete '{item.Name}'?",          // the title, with the project's name inside
            "This action cannot be undone.",   // the message
            confirmText: "Delete",             // the text of the confirm button
            isDestructive: true);              // paint the confirm button red

        // The user pressed Cancel
        if (!confirmed)
        {
            return;   // stop here - nothing more to do
        }

        // Ask the service to delete it, and wait
        var result = await _projectService.DeleteProjectAsync(item.Id);

        // e.g. it was already deleted - show why
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;   // stop here - nothing more to do
        }

        // If the deleted row was the selected one, clear the selection
        // (ReferenceEquals = "is it the very same object in memory?")
        if (ReferenceEquals(SelectedProject, item))
        {
            SelectedProject = null;
        }

        // Read the list again so the deleted project disappears
        await LoadProjectsAsync();

    });

    // Purpose: Called by the navigation service every time this screen is shown - loads the list.
    // Input:   nothing.
    // Output:  nothing.
    // The navigation service can't wait (OnNavigatedTo is not async), so the load starts here and
    // finishes on its own. "_ =" = we start the Task without waiting for it; ExecuteAsync still shows
    // the spinner while it runs, and turns any error into ErrorMessage instead of crashing
    void INavigationAware.OnNavigatedTo() => _ = ExecuteAsync(() => LoadProjectsAsync());


    // Purpose: Called by the navigation service when the user leaves this screen.
    // Input:   nothing.
    // Output:  nothing. There is nothing to clean up, but the interface requires the method
    void INavigationAware.OnNavigatedFrom()
    {
    }

    // Purpose: Runs every time SearchText changes (each typed letter) - rebuilds the rows.
    // Input:   value - the new search text.
    // Output:  nothing.
    // [ObservableProperty] calls a method with this exact name after the property changes
    partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();

    // Purpose: Runs every time SortMode changes - rebuilds the rows in the new order.
    // Input:   value - the new sort mode.
    // Output:  nothing.
    partial void OnSortModeChanged(ProjectSortMode value) => ApplyFilterAndSort();

    // Purpose: Reads the projects from the service and rebuilds the list on the screen.
    // Input:   nothing.
    // Output:  a Task that finishes when the list is ready.
    private async Task LoadProjectsAsync()
    {
        // "(await ...)" = first wait for the list, and only then turn it into a List
        _allProjects = (await _projectService.GetAllProjectsAsync()).ToList();

        // Apply the search and the sort, and fill the rows on the screen
        ApplyFilterAndSort();
    }


    // Purpose: Builds the rows on the screen from _allProjects, using the search text and the sort.
    // Input:   nothing (reads _allProjects, SearchText, SortMode).
    // Output:  nothing (fills Projects).
    private void ApplyFilterAndSort()
    {
        // The id of the open project, or null if none. "?." = if CurrentProject is null, the result is null
        var currentId = _projectService.CurrentProject?.Id;

        // Start from all the projects
        IEnumerable<Project> filtered = _allProjects;

        // If something is typed in the search box, keep only names that contain it (any upper/lower case)
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Sort: by name A-Z, or by last change (newest first)
        filtered = SortMode == ProjectSortMode.Name
            ? filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            : filtered.OrderByDescending(p => p.LastModifiedAtUtc);

        // Empty the rows on the screen
        Projects.Clear();

        // One row for every project that is left. The second argument: is this the open project?
        foreach (var project in filtered)
        {
            Projects.Add(new ProjectListItem(project, currentId is not null && project.Id.Equals(currentId.Value)));
        }

        // HasProjects / HasNoProjects are computed from the rows, so tell the screen they may have changed
        OnPropertyChanged(nameof(HasProjects));   // tell the screen that HasProjects may have changed
        OnPropertyChanged(nameof(HasNoProjects));   // tell the screen that HasNoProjects may have changed
    }
}
