using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.UI.Common;
using NetSim.UI.Navigation;

namespace NetSim.UI.ViewModels;

/// <summary>
/// Coordinates the application shell: owns navigation and exposes the ViewModel currently on
/// screen.
/// </summary>
// partial because CommunityToolkit.Mvvm's source generator adds a second half to this class at
// build time - that's where the [ObservableProperty] field below actually gets its public property
public partial class ShellViewModel : ViewModelBase
{
    // Typed as an interface (INavigationService), not a concrete class - this class only cares
    // that whatever it's given can navigate, not which specific implementation does it.
    private readonly INavigationService _navigationService;

    // The source generator turns this private field into a public CurrentViewModel property with
    // get/set - the exact same get/private-set/SetProperty pattern MainWindowViewModel.CurrentPage
    // was written by hand, just generated automatically instead of typed out here:
    //
    //     public ViewModelBase? CurrentViewModel
    //     {
    //         get => _currentViewModel;
    //         set => SetProperty(ref _currentViewModel, value);
    //     }
    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    // Constructor injection: this class doesn't build its dependency itself (no `new` anywhere
    // here) - it just declares what it needs, and whoever constructs a ShellViewModel has to
    // supply it. In practice that's the DI container, via GetRequiredService<ShellViewModel>()
    // in MainWindowViewModel.ShowWorkspace.
    public ShellViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;

        // Same event-subscription pattern as LoginViewModel.SignedIn: this class registers itself as
        // a listener so it finds out whenever the on-screen workspace page changes
        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;

        // Default screen shown the moment the workspace opens - mirrors ShowLogin() calling itself
        // once in MainWindowViewModel's constructor so there's always something on screen to start with
        _navigationService.NavigateTo<HomeViewModel>();
    }

    public bool CanGoBack => _navigationService.CanGoBack;

    public bool IsHomeActive => CurrentViewModel is HomeViewModel;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack() => _navigationService.GoBack();

    [RelayCommand]
    private void NavigateHome() => _navigationService.NavigateTo<HomeViewModel>();

                                           //who broadcast the event //extra information aboutwhat happend
    private void OnCurrentViewModelChanged(object? sender, System.EventArgs e)//every function that has been registered to event must look like that
    {
        CurrentViewModel = _navigationService.CurrentViewModel;
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsHomeActive));
        GoBackCommand.NotifyCanExecuteChanged();
    }
}
