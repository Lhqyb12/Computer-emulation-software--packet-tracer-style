using CommunityToolkit.Mvvm.Input;   // [RelayCommand]
using NetSim.UI.Common;              // ViewModelBase
using NetSim.UI.Navigation;          // INavigationService

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The Home screen - the first screen shown in the Shell after sign-in
public partial class HomeViewModel : ViewModelBase
{
    // Moving between screens (the buttons on Home lead to other screens)
    private readonly INavigationService _navigationService;

    // Purpose: Builds the Home screen.
    // Input:   navigationService - passed in by the DI container.
    // Output:  none (a constructor).
    public HomeViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;   // keep the navigation service
    }

    // The big title in the middle of the screen
    public string Title => "Network Simulator";

    // The sentence under the title
    public string Message => "MVVM infrastructure ready.";

    // Purpose: The "Go to Projects" button - shows the Projects screen.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void GoToProjects() => _navigationService.NavigateTo<ProjectsViewModel>();

    // Purpose: The "Create a class" card: opens the Classes screen and its "Create a class" dialog.
    // Input:   nothing.
    // Output:  nothing.
    // The create logic lives only in ClassroomsViewModel - Home just goes there and presses its
    // button, so the same code is never written twice
    [RelayCommand]
    private void CreateClassroom()
    {
        // Show the Classes screen
        _navigationService.NavigateTo<ClassroomsViewModel>();

        // "as" = the screen now showing, as a ClassroomsViewModel (null if it's something else).
        // "?." = only if it really is the Classes screen. Execute(null) = "press" its Create button
        (_navigationService.CurrentViewModel as ClassroomsViewModel)?.CreateClassroomCommand.Execute(null);
    }

    // Purpose: The "Join a class" card: opens the Classes screen and its "Join a class" dialog.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void JoinClassroom()
    {
        // Show the Classes screen
        _navigationService.NavigateTo<ClassroomsViewModel>();

        // "Press" its Join button (only if the Classes screen is really the one showing)
        (_navigationService.CurrentViewModel as ClassroomsViewModel)?.JoinClassroomCommand.Execute(null);
    }
}
