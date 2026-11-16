using System;                           // EventArgs
using CommunityToolkit.Mvvm.Input;      // [RelayCommand] - turns a method into a Command a button can call
using NetSim.Application.State;         // IApplicationState - knows which project is open
using NetSim.UI.Common;                 // ViewModelBase - the base class of every screen
using NetSim.UI.Navigation;             // INavigationService, INavigationAware - moving between screens

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The Network Workspace screen: the place where the user builds the network of the open project.
// It holds the Device Library panel (left) and the Network Canvas (middle), and knows whether a
// project is open. The device properties panel will be added in part 2 of the plan.
// "partial" = CommunityToolkit.Mvvm writes a second half of this class at build time (for [RelayCommand]).
// INavigationAware = the navigation service tells this screen when it is shown and when it is left
public partial class NetworkWorkspaceViewModel : ViewModelBase, INavigationAware
{
    private readonly IApplicationState _applicationState;     // which project is open
    private readonly INavigationService _navigationService;   // moving between screens

    // Purpose:       Builds the screen and starts listening to "the open project changed".
    // Precondition:  deviceLibrary, canvas, applicationState and navigationService are not null
    //                (the DI container passes them in).
    // Postcondition: the screen holds the two panels and listens to CurrentProjectChanged until it is left.
    public NetworkWorkspaceViewModel(
        DeviceLibraryViewModel deviceLibrary,
        NetworkCanvasViewModel canvas,
        IApplicationState applicationState,
        INavigationService navigationService)
    {
        DeviceLibrary = deviceLibrary;            // keep the Device Library panel
        Canvas = canvas;                          // keep the Network Canvas (new in step 3)
        _applicationState = applicationState;     // keep the application state
        _navigationService = navigationService;   // keep the navigation service

        // "+=" = when this event is raised, run OnCurrentProjectChanged.
        // Change: a named method instead of a small inline function, so it can be removed again with "-="
        // when the screen is left (see OnNavigatedFrom)
        _applicationState.CurrentProjectChanged += OnCurrentProjectChanged;
    }

    // Is a project open? false = the screen shows "No project is currently open" instead of the workspace.
    // "=>" = computed every time it is read, from the application state
    public bool HasCurrentProject => _applicationState.CurrentProject is not null;

    // The Device Library panel on the left side. The view shows it with DeviceLibraryView
    public DeviceLibraryViewModel DeviceLibrary { get; }

    // The Network Canvas in the middle. The view shows it with NetworkCanvasControl (step 3)
    public NetworkCanvasViewModel Canvas { get; }

    // Purpose:       The "Go to Projects" button (shown when no project is open) - moves to the Projects screen.
    // Precondition:  none.
    // Postcondition: the Projects screen is shown.
    [RelayCommand]
    private void GoToProjects() => _navigationService.NavigateTo<ProjectsViewModel>();

    // Purpose:       Called by the navigation service every time this screen is shown.
    // Precondition:  none.
    // Postcondition: the screen read HasCurrentProject again, in case a project was opened or closed
    //                while we were on another screen.
    void INavigationAware.OnNavigatedTo() => OnPropertyChanged(nameof(HasCurrentProject));

    // Purpose:       Called by the navigation service when the user leaves this screen - stops listening.
    // Precondition:  none.
    // Postcondition: neither this screen nor its canvas listens to the Application layer any more.
    //                Why: those states live for the whole sign-in, but a new workspace screen is made
    //                every time it is opened - without this, old screens would keep listening forever.
    void INavigationAware.OnNavigatedFrom()
    {
        _applicationState.CurrentProjectChanged -= OnCurrentProjectChanged;   // "-=" = stop listening
        Canvas.Detach();                                                      // the canvas stops listening too
    }

    // Purpose:       Runs when a project is opened or closed.
    // Precondition:  none (called by CurrentProjectChanged).
    // Postcondition: the screen was told that HasCurrentProject may have changed.
    private void OnCurrentProjectChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(HasCurrentProject));
}
