using System.Threading.Tasks;                // Task - "a job that will finish later"
using CommunityToolkit.Mvvm.ComponentModel;  // [ObservableProperty] - writes the public property for a private field
using CommunityToolkit.Mvvm.Input;           // [RelayCommand] - turns a method into a Command a button can call
using NetSim.Application.Services;           // IProjectService - the project rules
using NetSim.UI.Common;                      // ViewModelBase - IsBusy, ErrorMessage, ExecuteAsync
using NetSim.UI.Dialogs;                     // IDialogService, IDialogHost - the pop-up windows
using NetSim.UI.Navigation;                  // INavigationService - moving between screens

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The "Shell" = the frame shown after sign-in: the menu, the toolbar, the sidebar, the status bar,
// and in the middle the current screen (Home / Projects / Classes). It owns the navigation, and the
// project commands that work from every screen (New / Open / Save / Save As / Close).
// "partial" = CommunityToolkit.Mvvm writes a second half of this class at build time - that's where
// the [ObservableProperty] field and the [RelayCommand] methods below get their public parts
public partial class ShellViewModel : ViewModelBase
{
    // Typed as interfaces, not concrete classes - this class only cares that whatever it's given can
    // navigate / manage projects / show dialogs, not which class does it. The DI container decides
    private readonly INavigationService _navigationService;   // moving between screens
    private readonly IProjectService _projectService;         // the project rules
    private readonly IDialogService _dialogService;           // the pop-up windows

    // The screen shown in the middle. [ObservableProperty] turns this private field into a public
    // CurrentViewModel property with get/set that tells the screen when it changes
    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    // Purpose: Builds the Shell, listens to navigation and project changes, and shows Home first.
    // Input:   navigationService, projectService, dialogService, dialogHost - passed in by the DI container.
    // Output:  none (a constructor).
    // This class doesn't build any of its four dependencies itself (no "new" here) - it just declares
    // what it needs, and the DI container supplies them (MainWindowViewModel asks the container for a Shell)
    public ShellViewModel(INavigationService navigationService, IProjectService projectService, IDialogService dialogService, IDialogHost dialogHost)
    {
        _navigationService = navigationService;   // keep the navigation service
        _projectService = projectService;         // keep the project service
        _dialogService = dialogService;           // keep the dialog service
        DialogHost = dialogHost;                  // keep the dialog host (the screen binds the pop-up layer to it)

        // "+=" = when this event is raised, call this method. Three things to listen to:
        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;     // the screen in the middle changed
        _projectService.CurrentProjectChanged += (_, _) => OnProjectStateChanged();  // the open project changed
        _projectService.DirtyStateChanged += (_, _) => OnProjectStateChanged();      // "unsaved changes" changed

        // The first screen shown when the Shell opens, so the middle is never empty
        _navigationService.NavigateTo<HomeViewModel>();
    }

    // Holds the pop-up window that is open right now (or none). The pop-up layer on the screen binds to it
    public IDialogHost DialogHost { get; }

    // Is there a screen to go back to? Passed on from the navigation service
    public bool CanGoBack => _navigationService.CanGoBack;

    // Which screen is in the middle - used to highlight the matching button in the sidebar.
    // "is X" = "the object is of type X"
    public bool IsHomeActive => CurrentViewModel is HomeViewModel;              // Home is shown
    public bool IsProjectsActive => CurrentViewModel is ProjectsViewModel;      // Projects is shown
    public bool IsClassroomsActive => CurrentViewModel is ClassroomsViewModel;  // Classes is shown
    public bool IsWorkspaceActive => CurrentViewModel is NetworkWorkspaceViewModel;  // the Network Workspace is shown


    // Is a project open? Save / Save As / Close work only while this is true
    public bool HasCurrentProject => _projectService.CurrentProject is not null;

    // The text on the right of the status bar: "No project open", or "Project: <name>" with
    // " *" at the end when there are unsaved changes. "a ? b : c" = if a then b, otherwise c
    public string ProjectStatusText => _projectService.CurrentProject is null
        ? "No project open"
        : $"Project: {_projectService.CurrentProject.Name}{(_projectService.IsCurrentProjectDirty ? " *" : "")}";

    // Purpose: The toolbar's Back button - returns to the previous screen.
    // Input:   nothing.
    // Output:  nothing.
    // CanExecute = the button works only while CanGoBack is true
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack() => _navigationService.GoBack();

    // Purpose: The sidebar's "Home" button - shows the Home screen.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void NavigateHome() => _navigationService.NavigateTo<HomeViewModel>();

    // Purpose: The sidebar's "Projects" button - shows the Projects screen.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void NavigateProjects() => _navigationService.NavigateTo<ProjectsViewModel>();

    // Purpose: The sidebar's "Classes" button - shows the Classes screen.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void NavigateClassrooms() => _navigationService.NavigateTo<ClassroomsViewModel>();

    // Purpose: The sidebar's "Network" button - shows the Network Workspace screen.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void NavigateWorkspace() => _navigationService.NavigateTo<NetworkWorkspaceViewModel>();


    // Purpose: "New Project" from the menu / toolbar - asks for a name and creates the project.
    // Input:   nothing.
    // Output:  a Task that finishes when the work is done (or the user cancelled).
    // ExecuteAsync (ViewModelBase) shows "busy", clears the old error, and turns exceptions into ErrorMessage
    [RelayCommand]
    private Task NewProjectAsync() => ExecuteAsync(async () =>
    {
        // Open the "New Project" window and wait for Create / Cancel
        var result = await _dialogService.ShowNewProjectAsync();

        // null = Cancel
        if (result is null)
        {
            return;   // stop here - nothing more to do
        }

        // Ask the service to create it, and wait
        var creation = await _projectService.CreateProjectAsync(result.Name, result.Description);

        // No error on success, the message on failure
        ErrorMessage = creation.IsSuccess ? null : creation.ErrorMessage;

        // It worked - go straight to the workspace, so the user can start building the new project's network
        if (creation.IsSuccess)
        {
            _navigationService.NavigateTo<NetworkWorkspaceViewModel>();   // show the Network Workspace
        }

    });

    // Purpose: "Open Project" from the menu / toolbar - goes to the Projects screen, where the user picks one.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void OpenProject() => _navigationService.NavigateTo<ProjectsViewModel>();

    // Purpose: "Save" from the menu / toolbar - saves the open project.
    // Input:   nothing.
    // Output:  a Task that finishes when it is saved.
    // CanExecute = the button works only while HasCurrentProject is true (a project is open)
    [RelayCommand(CanExecute = nameof(HasCurrentProject))]
    private Task SaveProjectAsync() => ExecuteAsync(async () =>
    {
        // Ask the service to save, and wait
        var result = await _projectService.SaveCurrentProjectAsync();

        // No error on success, the message on failure
        ErrorMessage = result.IsSuccess ? null : result.ErrorMessage;
    });


    // Purpose: "Save As" - asks for a new name and saves a copy of the open project under it.
    // Input:   nothing.
    // Output:  a Task that finishes when the copy is saved (or the user cancelled).
    [RelayCommand(CanExecute = nameof(HasCurrentProject))]
    private Task SaveProjectAsAsync() => ExecuteAsync(async () =>
    {
        // The open project's name, to fill the window. "?." and "??" = an empty text if nothing is open
        var currentName = _projectService.CurrentProject?.Name ?? string.Empty;

        // Reuse the Rename window to ask for the new name, and wait
        var newName = await _dialogService.ShowRenameProjectAsync(currentName);

        // null = Cancel
        if (newName is null)
        {
            return;   // stop here - nothing more to do
        }

        // Ask the service to save the copy, and wait
        var result = await _projectService.SaveCurrentProjectAsAsync(newName);

        // No error on success, the message on failure
        ErrorMessage = result.IsSuccess ? null : result.ErrorMessage;
    });

    // Purpose: "Close Project" - closes the open project, asking first about unsaved changes.
    // Input:   nothing.
    // Output:  a Task that finishes when the project is closed (or the user cancelled).
    [RelayCommand(CanExecute = nameof(HasCurrentProject))]
    private Task CloseProjectAsync() => ExecuteAsync(() => HandleCloseCurrentProjectAsync());

    // Purpose: Closes the open project; if it has unsaved changes, asks Save / Don't Save / Cancel first.
    // Input:   nothing.
    // Output:  a Task with true if the project was closed (or none was open), false if the user cancelled
    //          or the save failed - so a caller (e.g. app shutdown, later) knows whether to continue.
    public async Task<bool> HandleCloseCurrentProjectAsync()
    {
        // The open project
        var project = _projectService.CurrentProject;

        // Nothing is open - nothing to close, the caller may continue
        if (project is null)
        {
            return true;   // tell the caller: closed (or nothing was open)
        }

        // Only ask if there are unsaved changes
        if (_projectService.IsCurrentProjectDirty)
        {
            // Show the three-button window and wait for the choice
            var decision = await _dialogService.ShowUnsavedChangesAsync(project.Name);

            // A different action for each choice ("Don't Save" has no case: it just goes on to close)
            switch (decision)
            {
                // Cancel - don't close
                case UnsavedChangesDecision.Cancel:
                    return false;   // tell the caller: not closed

                // Save - save first, and wait
                case UnsavedChangesDecision.Save:
                    var saveResult = await _projectService.SaveCurrentProjectAsync();

                    // The save failed - show why, and don't close (the changes would be lost)
                    if (!saveResult.IsSuccess)
                    {
                        ErrorMessage = saveResult.ErrorMessage;
                        return false;   // tell the caller: not closed
                    }

                    // leave the switch and go on to close
                    break;   // leave the loop / switch
            }
        }

        // Close it, and tell the caller it was closed
        _projectService.CloseCurrentProject();
        return true;   // tell the caller: closed (or nothing was open)
    }

    // Purpose: Runs when the open project or its "unsaved" flag changes - refreshes everything that depends on it.
    // Input:   nothing.
    // Output:  nothing.
    private void OnProjectStateChanged()
    {
        // These two are computed from the project service, so the screen is told by hand that they may have changed
        OnPropertyChanged(nameof(HasCurrentProject));   // tell the screen that HasCurrentProject may have changed
        OnPropertyChanged(nameof(ProjectStatusText));   // tell the screen that ProjectStatusText may have changed

        // Ask the three commands to check their CanExecute again, so the buttons turn on / off
        SaveProjectCommand.NotifyCanExecuteChanged();   // the SaveProject button checks again whether it may run (turns on / off)
        SaveProjectAsCommand.NotifyCanExecuteChanged();   // the SaveProjectAs button checks again whether it may run (turns on / off)
        CloseProjectCommand.NotifyCanExecuteChanged();   // the CloseProject button checks again whether it may run (turns on / off)
    }

    // Purpose: Runs when the navigation service shows a different screen - puts it in the middle.
    // Input:   sender - who raised the event (the navigation service); e - extra information (none here).
    // Output:  nothing.
    // Every method that listens to this kind of event must take exactly these two inputs
    private void OnCurrentViewModelChanged(object? sender, System.EventArgs e)
    {
        // Show the navigation service's current screen in the middle
        CurrentViewModel = _navigationService.CurrentViewModel;

        // These are computed, so tell the screen by hand that they may have changed
        OnPropertyChanged(nameof(CanGoBack));   // tell the screen that CanGoBack may have changed
        OnPropertyChanged(nameof(IsHomeActive));   // tell the screen that IsHomeActive may have changed
        OnPropertyChanged(nameof(IsProjectsActive));   // tell the screen that IsProjectsActive may have changed
        OnPropertyChanged(nameof(IsClassroomsActive));   // tell the screen that IsClassroomsActive may have changed
        OnPropertyChanged(nameof(IsWorkspaceActive));   // tell the screen that IsWorkspaceActive may have changed


        // The Back button checks CanGoBack again, so it turns on / off
        GoBackCommand.NotifyCanExecuteChanged();   // the GoBack button checks again whether it may run (turns on / off)
    }
}
