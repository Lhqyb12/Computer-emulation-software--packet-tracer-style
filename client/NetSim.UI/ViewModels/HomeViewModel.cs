using CommunityToolkit.Mvvm.Input;
using NetSim.UI.Common;
using NetSim.UI.Navigation;

namespace NetSim.UI.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public HomeViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public string Title => "Network Simulator";

    public string Message => "MVVM infrastructure ready.";

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
        _navigationService.NavigateTo<ClassroomsViewModel>();

        // "as" = the screen now showing, as a ClassroomsViewModel (null if it's something else).
        // "?." = only if it really is the Classes screen
        (_navigationService.CurrentViewModel as ClassroomsViewModel)?.CreateClassroomCommand.Execute(null);
    }

    // Purpose: The "Join a class" card: opens the Classes screen and its "Join a class" dialog.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void JoinClassroom()
    {
        _navigationService.NavigateTo<ClassroomsViewModel>();
        (_navigationService.CurrentViewModel as ClassroomsViewModel)?.JoinClassroomCommand.Execute(null);
    }
}
