using CommunityToolkit.Mvvm.Input;
using NetSim.Application.State;
using NetSim.UI.Common;
using NetSim.UI.Navigation;

namespace NetSim.UI.ViewModels;

/// <summary>
/// The Network Workspace screen: hosts the Network Canvas (<see cref="Canvas"/>) and the Device
/// Library (<see cref="DeviceLibrary"/>, Phase 11), scoped to whichever project is currently open
/// - see docs/architecture/network-canvas.md. Reachable from the sidebar, and entered
/// automatically after creating/opening a project (see ShellViewModel.NewProjectCommand,
/// ProjectsViewModel.OpenProjectCommand/NewProjectCommand).
/// </summary>
public partial class NetworkWorkspaceViewModel : ViewModelBase, INavigationAware
{
    private readonly IApplicationState _applicationState;
    private readonly INavigationService _navigationService;

    public NetworkWorkspaceViewModel(
        NetworkCanvasViewModel canvas,
        DeviceLibraryViewModel deviceLibrary,
        DevicePropertiesViewModel deviceProperties,
        IApplicationState applicationState,
        INavigationService navigationService)
    {
        Canvas = canvas;
        DeviceLibrary = deviceLibrary;
        DeviceProperties = deviceProperties;
        _applicationState = applicationState;
        _navigationService = navigationService;

        _applicationState.CurrentProjectChanged += (_, _) => OnPropertyChanged(nameof(HasCurrentProject));
    }

    public NetworkCanvasViewModel Canvas { get; }

    public DeviceLibraryViewModel DeviceLibrary { get; }

    /// <summary>The Device Properties panel (Phase 12) - mirrors whichever device is selected on <see cref="Canvas"/>.</summary>
    public DevicePropertiesViewModel DeviceProperties { get; }

    /// <summary>False when no project is open - the view shows a "No project is currently open" state instead of the canvas.</summary>
    public bool HasCurrentProject => _applicationState.CurrentProject is not null;

    [RelayCommand]
    private void GoToProjects() => _navigationService.NavigateTo<ProjectsViewModel>();

    void INavigationAware.OnNavigatedTo() => OnPropertyChanged(nameof(HasCurrentProject));

    void INavigationAware.OnNavigatedFrom()
    {
    }
}
