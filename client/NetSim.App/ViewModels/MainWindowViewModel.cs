using System;
using CommunityToolkit.Mvvm.Input;               // RelayCommand / IRelayCommand - a ready-made "invoke this method" command, bindable from XAML
using Microsoft.Extensions.DependencyInjection;    // GetRequiredService<T>() - ask the DI container to build/hand us an instance of T
using NetSim.UI.ViewModels;                        // ShellViewModel - the signed-in simulator workspace, ported from NetSim.UI

namespace NetSim.App.ViewModels;

/// <summary>
/// The view model for the whole window. Its only job is <b>navigation</b>:
/// it holds whichever screen is currently visible (login / register / the signed-in
/// simulator workspace) in <see cref="CurrentPage"/>, and swaps it when asked.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    // Saved for later use in ShowSignedIn: building a ShellViewModel means building a whole tree of
    // other services underneath it (canvas engine, device services, etc.), and the DI container
    // already knows how to do that - we don't want to wire that tree up by hand here
    private readonly IServiceProvider _services;

    private object _currentPage = null!;
    private string? _signedInEmail;
    private string? _signedInRole;
    private string? _signedInToken;


    public MainWindowViewModel(IServiceProvider services) //constructor
    {
        _services = services;

        // Wraps ShowLogin so it can be bound to a button's Command in XAML (the "Sign out" button) -
        // in MVVM the View never calls C# methods directly, it only binds to Commands/Properties
        SignOutCommand = new RelayCommand(ShowLogin);
        ManageUsersCommand = new RelayCommand(ShowUsers);

        // Runs once so the app actually has something to show on first launch; without this,
        // CurrentPage would still be null when the window first renders
        ShowLogin();
    }

    public IRelayCommand SignOutCommand { get; }
    public IRelayCommand ManageUsersCommand { get; }

    /// <summary>The screen shown right now. The window binds its content to this. Typed as
    /// <see cref="object"/> because the auth screens (<see cref="ViewModelBase"/>) and the
    /// simulator workspace ported from NetworkSimulator (<c>NetSim.UI.Common.ViewModelBase</c>)
    /// are unrelated view-model hierarchies - see the two ViewLocators in App.axaml.</summary>
    public object CurrentPage
    {
        get => _currentPage;
        // Private on purpose - which screen is showing is this ViewModel's own decision, nothing
        // outside this class should be able to force a screen change directly
        private set
        {
            // SetProperty (from the base ViewModelBase) stores the value and raises
            // PropertyChanged("CurrentPage") - this is literally how Avalonia's binding system
            // knows to refresh the window and display the new page
            SetProperty(ref _currentPage, value);

            // IsWorkspaceActive/IsAuthActive are computed from CurrentPage below, but C# has no way
            // of knowing that automatically - changing CurrentPage doesn't by itself notify anything
            // bound to these two, so we raise their change notifications by hand here
            OnPropertyChanged(nameof(IsWorkspaceActive));
            OnPropertyChanged(nameof(IsAuthActive));
        }
    }

    /// <summary>True once signed in - the window shows the full-window simulator workspace
    /// instead of the narrow branding+card auth layout.</summary>
    // Computed rather than a stored bool flag, so it can never drift out of sync with reality -
    // CurrentPage is the single source of truth, this just asks what's currently sitting in it
    public bool IsWorkspaceActive => CurrentPage is ShellViewModel or UsersViewModel;


    // The exact opposite of IsWorkspaceActive - kept as its own property because the XAML needs to
    // bind to both directions (auth layout vs. workspace layout), which reads cleaner than negating
    // IsWorkspaceActive inside markup
    public bool IsAuthActive => !IsWorkspaceActive;

    public string? SignedInEmail
    {
        get => _signedInEmail;
        private set => SetProperty(ref _signedInEmail, value);
    }
    public string? SignedInRole
    {
        get => _signedInRole;
        private set
        {
            SetProperty(ref _signedInRole, value);
            OnPropertyChanged(nameof(IsAdmin));
        }
    }

    /// <summary>True when the signed-in user may access the "Manage users" screen.</summary>
    public bool IsAdmin => SignedInRole == "Admin";


    private void ShowLogin()
    {
        // Also reached from sign-out (via SignOutCommand), so this has to actively reset state back
        // to "nobody signed in" - it's not just a startup-only path
        SignedInEmail = null;
        SignedInRole = null;
        _signedInToken = null;


        // A fresh instance every time (not resolved from DI - LoginViewModel has no dependencies of
        // its own here) so that any half-filled form state doesn't linger if the user comes back to
        // this screen later
        var login = new LoginViewModel();

        // LoginViewModel doesn't know anything about navigation or about this class - it just raises
        // an event when the user wants to switch screens, and whoever owns navigation (us) decides
        // what that actually means. This is what keeps the screen and the navigation logic decoupled
        login.SwitchToRegisterRequested += ShowRegister;
        login.SignedIn += ShowSignedIn;
        login.SwitchToForgotPasswordRequested += ShowForgotPassword;

        CurrentPage = login;
    }

    private void ShowRegister()
    {
        // Same pattern as ShowLogin: a fresh instance, and listen for "user wants to go back to login"
        var register = new RegisterViewModel();
        register.SwitchToLoginRequested += ShowLogin;
        CurrentPage = register;
    }

     private void ShowForgotPassword()
    {
        var forgot = new ForgotPasswordViewModel();
        forgot.SwitchToLoginRequested += ShowLogin;
        CurrentPage = forgot;
    }


    private void ShowSignedIn(string email, string role, string token)
    {
        SignedInEmail = email;
        SignedInRole = role;
        _signedInToken = token;
        ShowWorkspace();
    }


    private void ShowWorkspace()
    {
        // GetRequiredService walks the dependency graph registered at startup in CompositionRoot and
        // constructs everything ShellViewModel needs underneath it automatically - building that by
        // hand ("new ShellViewModel(...)") would mean knowing and wiring up its entire service tree
        // ourselves, which is exactly the problem a DI container exists to solve. "Required" means it
        // throws immediately if something's missing from the container, instead of returning null.
        //
        // Hand off to the simulator workspace ported from NetworkSimulator (stage 1: UI only,
        // in-memory - no server/database wiring yet)
        CurrentPage = _services.GetRequiredService<ShellViewModel>();
    }

    private void ShowUsers()
    {
        if ( _signedInToken is null)
            return;

         var users = new UsersViewModel( _signedInToken);
        // When the user clicks "Back" on the users screen, go back to the simulator workspace
        users.BackRequested += ShowWorkspace;
        CurrentPage = users;
    }

}
