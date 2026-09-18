using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.Application.Services;
using NetSim.UI.Common;
using NetSim.UI.Dialogs;
using NetSim.UI.Navigation;

namespace NetSim.UI.ViewModels;

/// <summary>
/// Coordinates the application shell: owns navigation and exposes the ViewModel currently on
/// screen, plus the project-lifecycle commands (New/Open/Save/Save As/Close) that are global to
/// the shell (menu, toolbar, status bar) rather than scoped to the Project Management screen -
/// see docs/architecture/project-management.md for why this split exists.
/// </summary>
// partial because CommunityToolkit.Mvvm's source generator adds a second half to this class at
// build time - that's where the [ObservableProperty] field below actually gets its public property
public partial class ShellViewModel : ViewModelBase
{
    // Typed as interfaces (INavigationService/IProjectService/IDialogService), not concrete classes -
    // this class only cares that whatever it's given can navigate/manage projects/show dialogs, not
    // which specific implementation does it. CompositionRoot.AddApplicationServices()/AddUIServices()
    // decide what actually gets built and handed in here.
    private readonly INavigationService _navigationService;
    private readonly IProjectService _projectService;
    private readonly IDialogService _dialogService;

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

    // Constructor injection: this class doesn't build any of its four dependencies itself (no `new`
    // anywhere here) - it just declares what it needs, and whoever constructs a ShellViewModel has
    // to supply them. In practice that's the DI container, via GetRequiredService<ShellViewModel>()
    // in MainWindowViewModel.ShowSignedIn.
    public ShellViewModel(INavigationService navigationService, IProjectService projectService, IDialogService dialogService, IDialogHost dialogHost)
    {
        _navigationService = navigationService;
        _projectService = projectService;
        _dialogService = dialogService;
        DialogHost = dialogHost;

        // Same event-subscription pattern as LoginViewModel.SignedIn: this class registers itself as
        // a listener so it finds out whenever the on-screen workspace page changes, project state
        // changes, or the dirty (unsaved-changes) flag flips
        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;
        _projectService.CurrentProjectChanged += (_, _) => OnProjectStateChanged();
        _projectService.DirtyStateChanged += (_, _) => OnProjectStateChanged();

        // Default screen shown the moment the workspace opens - mirrors ShowLogin() calling itself
        // once in MainWindowViewModel's constructor so there's always something on screen to start with
        _navigationService.NavigateTo<HomeViewModel>();
    }

    public IDialogHost DialogHost { get; }

    public bool CanGoBack => _navigationService.CanGoBack;

    public bool IsHomeActive => CurrentViewModel is HomeViewModel;
    public bool IsProjectsActive => CurrentViewModel is ProjectsViewModel;
    public bool IsWorkspaceActive => CurrentViewModel is NetworkWorkspaceViewModel;
    public bool IsSettingsActive => CurrentViewModel is SettingsViewModel;
    public bool IsDesignSystemActive => CurrentViewModel is DesignSystemDemoViewModel;

    public bool HasCurrentProject => _projectService.CurrentProject is not null;

    public string ProjectStatusText => _projectService.CurrentProject is null
        ? "No project open"
        : $"Project: {_projectService.CurrentProject.Name}{(_projectService.IsCurrentProjectDirty ? " *" : "")}";

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack() => _navigationService.GoBack();

    [RelayCommand]
    private void NavigateHome() => _navigationService.NavigateTo<HomeViewModel>();

    [RelayCommand]
    private void NavigateProjects() => _navigationService.NavigateTo<ProjectsViewModel>();

    [RelayCommand]
    private void NavigateWorkspace() => _navigationService.NavigateTo<NetworkWorkspaceViewModel>();

    [RelayCommand]
    private void NavigateSettings() => _navigationService.NavigateTo<SettingsViewModel>();

    [RelayCommand]
    private void NavigateDesignSystem() => _navigationService.NavigateTo<DesignSystemDemoViewModel>();

    [RelayCommand]
    private Task NewProjectAsync() => ExecuteAsync(async () =>
    {
        var result = await _dialogService.ShowNewProjectAsync();
        if (result is null)
        {
            return;
        }

        var creation = _projectService.CreateProject(result.Name, result.Description);
        ErrorMessage = creation.IsSuccess ? null : creation.ErrorMessage;

        if (creation.IsSuccess)
        {
            _navigationService.NavigateTo<NetworkWorkspaceViewModel>();
        }
    });

    [RelayCommand]
    private void OpenProject() => _navigationService.NavigateTo<ProjectsViewModel>();

    [RelayCommand(CanExecute = nameof(HasCurrentProject))]
    private Task SaveProjectAsync() => ExecuteAsync(() =>
    {
        var result = _projectService.SaveCurrentProject();
        ErrorMessage = result.IsSuccess ? null : result.ErrorMessage;
        return Task.CompletedTask;
    });

    [RelayCommand(CanExecute = nameof(HasCurrentProject))]
    private Task SaveProjectAsAsync() => ExecuteAsync(async () =>
    {
        var currentName = _projectService.CurrentProject?.Name ?? string.Empty;
        var newName = await _dialogService.ShowRenameProjectAsync(currentName);
        if (newName is null)
        {
            return;
        }

        var result = _projectService.SaveCurrentProjectAs(newName);
        ErrorMessage = result.IsSuccess ? null : result.ErrorMessage;
    });

    [RelayCommand(CanExecute = nameof(HasCurrentProject))]
    private Task CloseProjectAsync() => ExecuteAsync(() => HandleCloseCurrentProjectAsync());

    /// <summary>
    /// Shared by the explicit "Close Project" command and application shutdown. Returns false if
    /// the user cancelled (so the caller must not proceed with closing/exiting).
    /// </summary>
    public async Task<bool> HandleCloseCurrentProjectAsync()
    {
        var project = _projectService.CurrentProject;
        if (project is null)
        {
            return true;
        }

        if (_projectService.IsCurrentProjectDirty)
        {
            var decision = await _dialogService.ShowUnsavedChangesAsync(project.Name);

            switch (decision)
            {
                case UnsavedChangesDecision.Cancel:
                    return false;
                case UnsavedChangesDecision.Save:
                    var saveResult = _projectService.SaveCurrentProject();
                    if (!saveResult.IsSuccess)
                    {
                        ErrorMessage = saveResult.ErrorMessage;
                        return false;
                    }

                    break;
            }
        }

        _projectService.CloseCurrentProject();
        return true;
    }

    private void OnProjectStateChanged()
    {
        OnPropertyChanged(nameof(HasCurrentProject));
        OnPropertyChanged(nameof(ProjectStatusText));
        SaveProjectCommand.NotifyCanExecuteChanged();
        SaveProjectAsCommand.NotifyCanExecuteChanged();
        CloseProjectCommand.NotifyCanExecuteChanged();
    }

    private void OnCurrentViewModelChanged(object? sender, System.EventArgs e)
    {
        CurrentViewModel = _navigationService.CurrentViewModel;
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsHomeActive));
        OnPropertyChanged(nameof(IsProjectsActive));
        OnPropertyChanged(nameof(IsWorkspaceActive));
        OnPropertyChanged(nameof(IsSettingsActive));
        OnPropertyChanged(nameof(IsDesignSystemActive));
        GoBackCommand.NotifyCanExecuteChanged();
    }
}
