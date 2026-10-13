using System;                                      // IServiceProvider
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

    // The screen shown right now. "= null!" = it starts as null, but we promise the compiler it is
    // filled before anyone reads it (the constructor calls ShowLogin)
    private object _currentPage = null!;

    // Who is signed in. "string?" = can be null; null = nobody is signed in
    private string? _signedInEmail;

    // The role of the signed-in user: "User" or "Admin"
    private string? _signedInRole;

    // The JWT the server gave at login; sent with the admin requests
    private string? _signedInToken;


    // The constructor. services = the DI container built in App.axaml.cs
    public MainWindowViewModel(IServiceProvider services)
    {
        // Keep the container for ShowWorkspace
        _services = services;

        // Wraps ShowLogin so it can be bound to a button's Command in XAML (the "Sign out" button) -
        // in MVVM the View never calls C# methods directly, it only binds to Commands/Properties
        SignOutCommand = new RelayCommand(ShowLogin);

        // The same for the "Manage users" button
        ManageUsersCommand = new RelayCommand(ShowUsers);

        // Runs once so the app actually has something to show on first launch; without this,
        // CurrentPage would still be null when the window first renders
        ShowLogin();
    }

    // The commands the buttons of the top bar are bound to. "{ get; }" = set once in the constructor
    public IRelayCommand SignOutCommand { get; }       // the "Sign out" button
    public IRelayCommand ManageUsersCommand { get; }   // the "Manage users" button (admins only)

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
            // Inside "set", the word "value" is the new value that was assigned.
            // SetProperty (from the base ViewModelBase) stores the value and raises
            // PropertyChanged("CurrentPage") - this is literally how Avalonia's binding system
            // knows to refresh the window and display the new page.
            // "ref" hands SetProperty the field itself, not a copy of its value, so it can change it
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
    // CurrentPage is the single source of truth, this just asks what's currently sitting in it.
    // "is X or Y" = the current page is an object of class X or of class Y
    public bool IsWorkspaceActive => CurrentPage is ShellViewModel or UsersViewModel;


    // The exact opposite of IsWorkspaceActive - kept as its own property because the XAML needs to
    // bind to both directions (auth layout vs. workspace layout), which reads cleaner than negating
    // IsWorkspaceActive inside markup
    public bool IsAuthActive => !IsWorkspaceActive;

    // The email of the signed-in user, shown in the top bar. null = nobody is signed in
    public string? SignedInEmail
    {
        get => _signedInEmail;
        private set => SetProperty(ref _signedInEmail, value);
    }

    // The role of the signed-in user
    public string? SignedInRole
    {
        get => _signedInRole;
        private set
        {
            // Store the new role and tell the screen
            SetProperty(ref _signedInRole, value);

            // IsAdmin is computed from the role, so the screen is told by hand that it changed too
            OnPropertyChanged(nameof(IsAdmin));
        }
    }

    /// <summary>True when the signed-in user may access the "Manage users" screen.</summary>
    public bool IsAdmin => SignedInRole == "Admin";


    // Shows the sign-in screen
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
        // what that actually means. This is what keeps the screen and the navigation logic decoupled.
        // "+=" = when this event is raised, call this method
        login.SwitchToRegisterRequested += ShowRegister;              // "Create an account" was pressed
        login.SignedIn += ShowSignedIn;                               // the login succeeded
        login.SwitchToForgotPasswordRequested += ShowForgotPassword;  // "Forgot password?" was pressed
        login.VerificationRequired += ShowVerifyEmail;                // the email still has to be verified


        // Put the screen in the window
        CurrentPage = login;
    }

    // Shows the "create account" screen
    private void ShowRegister()
    {
        // Same pattern as ShowLogin: a fresh instance, and listen for "user wants to go back to login"
        var register = new RegisterViewModel();
        register.SwitchToLoginRequested += ShowLogin;

        // After the account is created, continue to the "enter your code" screen
        register.VerificationRequired += ShowVerifyEmail;

        // Put the screen in the window
        CurrentPage = register;
    }


    // Shows the "enter the code we emailed you" screen. email = the address that has to be verified
    private void ShowVerifyEmail(string email)
    {
        // The register screen hands us the email through its VerificationRequired event, and we pass
        // it on to the new screen's constructor - so the user doesn't have to type it a second time
        var verify = new VerifyEmailViewModel(email);

        // After a successful verification (or "Back"), return to the sign-in screen
        verify.SwitchToLoginRequested += ShowLogin;

        // Put the screen in the window
        CurrentPage = verify;
    }


    // Shows the "forgot my password" screen
     private void ShowForgotPassword()
    {
        // A fresh instance, and listen for "go back to the sign-in screen"
        var forgot = new ForgotPasswordViewModel();
        forgot.SwitchToLoginRequested += ShowLogin;

        // Put the screen in the window
        CurrentPage = forgot;
    }


    // Called by the SignedIn event of the login screen, with the three values the server returned
    private void ShowSignedIn(string email, string role, string token)
    {
        // Remember who signed in
        SignedInEmail = email;
        SignedInRole = role;
        _signedInToken = token;

        // And open the simulator
        ShowWorkspace();
    }


    // Shows the simulator workspace
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

    // Shows the admin's "Manage users" screen
    private void ShowUsers()
    {
        // Without a token the server would refuse anyway, so there is nothing to show
        if ( _signedInToken is null)
            return;

        // The screen gets the token so it can prove to the server that an admin is asking
         var users = new UsersViewModel( _signedInToken);

        // When the user clicks "Back" on the users screen, go back to the simulator workspace
        users.BackRequested += ShowWorkspace;

        // Put the screen in the window
        CurrentPage = users;
    }

}
