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
}
