using CommunityToolkit.Mvvm.Input;      // [RelayCommand] - turns a method into a Command a button can call
using NetSim.Application.State;         // IApplicationState - knows which project is open
using NetSim.UI.Common;                 // ViewModelBase - the base class of every screen
using NetSim.UI.Navigation;             // INavigationService, INavigationAware - moving between screens

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The Network Workspace screen: the place where the user builds the network of the open project.
// It holds the Device Library panel and knows whether a project is open. The canvas and the
// device properties panel will be added to it in the next stages.
// "partial" = CommunityToolkit.Mvvm writes a second half of this class at build time (for [RelayCommand]).
// INavigationAware = the navigation service tells this screen when it is shown and when it is left
public partial class NetworkWorkspaceViewModel : ViewModelBase, INavigationAware
{
    private readonly IApplicationState _applicationState;     // which project is open
    private readonly INavigationService _navigationService;   // moving between screens

        // Purpose: Builds the screen and starts listening to "the open project changed".
    // Input:   deviceLibrary, applicationState, navigationService - passed in by the DI container.
    // Output:  none (a constructor).
    public NetworkWorkspaceViewModel(
        DeviceLibraryViewModel deviceLibrary,
        IApplicationState applicationState,
        INavigationService navigationService)
    {
        DeviceLibrary = deviceLibrary;            // keep the Device Library panel
        _applicationState = applicationState;     // keep the application state
        _navigationService = navigationService;   // keep the navigation service

        // "+=" = when this event is raised, run this small function.
        // When a project is opened or closed, tell the screen that HasCurrentProject may have changed
        _applicationState.CurrentProjectChanged += (_, _) => OnPropertyChanged(nameof(HasCurrentProject));
    }

    // Is a project open? false = the screen shows "No project is currently open" instead of the workspace.
    // "=>" = computed every time it is read, from the application state
    public bool HasCurrentProject => _applicationState.CurrentProject is not null;

    // The Device Library panel on the left side. The view shows it with DeviceLibraryView
    public DeviceLibraryViewModel DeviceLibrary { get; }


    // Purpose: The "Go to Projects" button (shown when no project is open) - moves to the Projects screen.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void GoToProjects() => _navigationService.NavigateTo<ProjectsViewModel>();

    // Purpose: Called by the navigation service every time this screen is shown.
    // Input:   nothing.
    // Output:  nothing.
    // Reads HasCurrentProject again, in case a project was opened or closed while we were on another screen
    void INavigationAware.OnNavigatedTo() => OnPropertyChanged(nameof(HasCurrentProject));

    // Purpose: Called by the navigation service when the user leaves this screen.
    // Input:   nothing.
    // Output:  nothing (there is nothing to clean up yet).
    void INavigationAware.OnNavigatedFrom()
    {
    }
}
