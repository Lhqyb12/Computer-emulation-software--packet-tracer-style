using System;                           // brings in EventArgs - the details an event hands over
using CommunityToolkit.Mvvm.Input;      // brings in [RelayCommand] - turns a method into a Command a button can call
using NetSim.Application.State;         // brings in IApplicationState - knows which project is open
using NetSim.Core.Devices;              // brings in NetworkDevice - the device the canvas asks to show (step 9)
using NetSim.UI.Common;                 // brings in ViewModelBase - the base class of every screen
using NetSim.UI.Navigation;             // brings in INavigationService (moving between screens) and INavigationAware

namespace NetSim.UI.ViewModels;   // the "address" of this file: the ViewModels part of the UI layer

// The Network Workspace screen: the place where the user builds the network of the open project.
// It holds the Device Library panel (left), the Network Canvas (middle) and the Device Properties panel (right, step 9),
// and knows whether a project is open. It is the one that CONNECTS them: when the canvas asks for a device's properties
// (a double-click), this screen opens the properties panel on that device.
// "partial" = CommunityToolkit.Mvvm writes a second half of this class at build time (for [RelayCommand]).
// ": ViewModelBase" = it inherits ErrorMessage, IsBusy and "property changed".
// "INavigationAware" = the navigation service tells this screen when it is shown (OnNavigatedTo) and when it is left (OnNavigatedFrom)
public partial class NetworkWorkspaceViewModel : ViewModelBase, INavigationAware
{
    // "private readonly" = only this class sees them; set once in the constructor, never replaced
    private readonly IApplicationState _applicationState;     // which project is open
    private readonly INavigationService _navigationService;   // moving between screens

    // Purpose:       Builds the screen, and starts listening to "the open project changed" and to the canvas's
    //                "show the properties of this device".
    // Precondition:  receives deviceLibrary - the Device Library panel; canvas - the Network Canvas;
    //                deviceProperties - the Device Properties panel (step 9); applicationState - which project is open;
    //                navigationService - moving between screens (all passed in by the DI container; none may be null).
    // Postcondition: returns nothing (a constructor) - the screen holds the three panels and listens to the two events
    //                until it is left.
    public NetworkWorkspaceViewModel(
        DeviceLibraryViewModel deviceLibrary,
        NetworkCanvasViewModel canvas,
        DevicePropertiesViewModel deviceProperties,
        IApplicationState applicationState,
        INavigationService navigationService)
    {
        DeviceLibrary = deviceLibrary;            // keep the Device Library panel
        Canvas = canvas;                          // keep the Network Canvas (step 3)
        DeviceProperties = deviceProperties;      // keep the Device Properties panel (step 9)
        _applicationState = applicationState;     // keep the application state
        _navigationService = navigationService;   // keep the navigation service

        // "+=" = when the event is raised, run the named method on the right.
        // A NAMED method (not a small inline function) so it can be removed again with "-=" in OnNavigatedFrom
        _applicationState.CurrentProjectChanged += OnCurrentProjectChanged;      // a project opened / closed
        Canvas.DevicePropertiesRequested += OnDevicePropertiesRequested;          // step 9: a double-click on a device
    }

    // Is a project open? false = the screen shows "No project is currently open" instead of the workspace.
    // "=>" = computed every time it is read; "is not null" = there IS an open project
    public bool HasCurrentProject => _applicationState.CurrentProject is not null;

    // The Device Library panel on the left side. The view shows it with DeviceLibraryView. Only "get" = never replaced
    public DeviceLibraryViewModel DeviceLibrary { get; }

    // The Network Canvas in the middle. The view shows it with NetworkCanvasControl (step 3)
    public NetworkCanvasViewModel Canvas { get; }

    // The Device Properties panel on the right side (step 9). The view shows it with DevicePropertiesView, only while it is open
    public DevicePropertiesViewModel DeviceProperties { get; }

    // Purpose:       The "Go to Projects" button (shown when no project is open) - moves to the Projects screen.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - the Projects screen is shown. ([RelayCommand] makes "GoToProjectsCommand" for the button)
    [RelayCommand]
    private void GoToProjects() => _navigationService.NavigateTo<ProjectsViewModel>();   // <ProjectsViewModel> = which screen to show

    // Purpose:       Called by the navigation service every time this screen is shown.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - the screen read HasCurrentProject again, in case a project was opened or closed
    //                while we were on another screen.
    void INavigationAware.OnNavigatedTo() => OnPropertyChanged(nameof(HasCurrentProject));

    // Purpose:       Called by the navigation service when the user leaves this screen - stops listening.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - neither this screen nor its canvas nor its properties panel listens to the
    //                Application layer any more.
    //                Why: those states live for the whole sign-in, but a NEW workspace screen is made every time it is
    //                opened - without this, old screens would keep listening (and stay in memory) forever.
    void INavigationAware.OnNavigatedFrom()
    {
        _applicationState.CurrentProjectChanged -= OnCurrentProjectChanged;    // "-=" = stop listening to "project changed"
        Canvas.DevicePropertiesRequested -= OnDevicePropertiesRequested;        // step 9: stop listening to the canvas's double-clicks
        Canvas.Detach();                                                       // the canvas stops listening to its states
        DeviceProperties.Detach();                                             // step 9: and so does the properties panel
    }

    // Purpose:       Runs when a project is opened or closed.
    // Precondition:  receives sender, e - the event's values (not used); called by CurrentProjectChanged.
    // Postcondition: returns nothing - the screen was told that HasCurrentProject may have changed (it shows / hides the workspace).
    private void OnCurrentProjectChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(HasCurrentProject));

    // Purpose:       Step 9 - runs when the canvas asks for a device's properties (the user double-clicked it).
    // Precondition:  receives sender - who raised it (the canvas view model, not used); device - the double-clicked device.
    // Postcondition: returns nothing - the Device Properties panel is open on that device.
    private void OnDevicePropertiesRequested(object? sender, NetworkDevice device) => DeviceProperties.Open(device);
}
