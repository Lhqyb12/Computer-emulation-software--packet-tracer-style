using System;                      // Action, Exception
using System.Threading.Tasks;      // Task, async/await
using CommunityToolkit.Mvvm.Input; // RelayCommand, AsyncRelayCommand, IRelayCommand - what a button runs
using NetSim.App.Services;         // AuthApiClient, GoogleSignInService, AuthResult

namespace NetSim.App.ViewModels;

/// <summary>
/// Data and behaviour of the <b>sign in</b> screen. Talks to the server via <see cref="AuthApiClient"/>.
/// </summary>
// "sealed" = no class can inherit from it. ": ViewModelBase" = it gets SetProperty from the base class
public sealed class LoginViewModel : ViewModelBase
{
    // The object that sends our requests to the server
    private readonly AuthApiClient _api = new();

    // The object that runs "Sign in with Google" in the browser
    private readonly GoogleSignInService _google = new();


    // The "backing fields": the real storage behind the public properties further down
    private string _email = string.Empty;      // what is typed in the email box
    private string _password = string.Empty;   // what is typed in the password box
    private bool _isPasswordVisible;           // true = show the password as readable text
    private bool _isBusy;                      // true while we wait for the server
    private string? _error;                    // the red error text (null = no error)
    private string? _status;                   // the neutral status text (null = nothing to show)

    // The constructor: connects every button to the code it runs
    public LoginViewModel()
    {
        // AsyncRelayCommand = for actions that take time, like talking to the server.
        // First argument: the method to run. Second: when the button is enabled - only while not busy
        SignInCommand = new AsyncRelayCommand(SignInAsync, () => !IsBusy);

        // RelayCommand = for a quick action. These two only raise an event:
        // "?.Invoke()" = call whoever subscribed to the event, if anybody did
        GoToRegisterCommand = new RelayCommand(() => SwitchToRegisterRequested?.Invoke());
        GoToForgotPasswordCommand = new RelayCommand(() => SwitchToForgotPasswordRequested?.Invoke());

        // The "Continue with Google" button, also disabled while busy
        GoogleSignInCommand = new AsyncRelayCommand(SignInWithGoogleAsync, () => !IsBusy);


    }

    // This screen doesn't know or care who's listening, or what happens next - it just announces
    // "the user asked to switch to register" and lets whoever's subscribed (MainWindowViewModel)
    // decide what that means. Keeps this ViewModel free of any navigation knowledge.
    // An "event" is a list of methods to call when something happens; others add themselves with "+="


    /// <summary>Raised when the user asks to go to the register screen.</summary>
    public event Action? SwitchToRegisterRequested;

    /// <summary>Raised when the user asks to go to the forgot-password screen.</summary>
    public event Action? SwitchToForgotPasswordRequested;

    /// <summary>Raised when the password was correct but the email was never verified, carrying that email.</summary>
    public event Action<string>? VerificationRequired;



    // Action<string, string, string> instead of a plain Action because subscribers need three values:
    // the email that signed in, its role and its token.
    // MainWindowViewModel.ShowSignedIn(string email, string role, string token) is exactly shaped to
    // match this signature, which is what lets it be attached with += in the first place
    /// <summary>Raised after a successful sign-in, carrying the email, the role and the token.</summary>
    public event Action<string, string, string>? SignedIn;


    // The text of the email box. The screen reads it with "get" and writes every typed letter with "set".
    // SetProperty stores the value and tells the screen it changed; typing also clears the old error
    public string Email
    {
        get => _email;
        set { SetProperty(ref _email, value); Error = null; }
    }

    // The text of the password box; works the same way as Email
    public string Password
    {
        get => _password;
        set { SetProperty(ref _password, value); Error = null; }
    }

    // true = the password is shown as readable text; switched by the "eye" button
    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
    }

    /// <summary>True while a request is in flight – disables the button and shows a spinner.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        // "private set" = only this class can change it
        private set
        {
            // Store the new value and tell the screen
            SetProperty(ref _isBusy, value);

            // Tell both buttons to ask again whether they are enabled (their rule is "!IsBusy")
            SignInCommand.NotifyCanExecuteChanged();
            GoogleSignInCommand.NotifyCanExecuteChanged();

        }
    }

    // The error text shown in red. null = no error
    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    // A neutral message, e.g. "Complete the sign-in in your browser...". null = nothing to show
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    // The commands the buttons of the screen are bound to. "{ get; }" = set once in the constructor
    public IRelayCommand SignInCommand { get; }              // the "Sign in" button
    public IRelayCommand GoToRegisterCommand { get; }        // the "Create an account" link
    public IRelayCommand GoToForgotPasswordCommand { get; }  // the "Forgot password?" link
    public IRelayCommand GoogleSignInCommand { get; }        // the "Continue with Google" button



    // Runs when the user presses "Sign in"
    private async Task SignInAsync()
    {
        // Clear the messages of the previous attempt
        Status = null;
        Error = null;

        // A quick check before bothering the server: something was typed and it has an "@"
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            Error = "Please enter a valid email address.";
            return;
        }

        // The password box must not be empty
        if (string.IsNullOrEmpty(Password))
        {
            Error = "Please enter your password.";
            return;
        }

        // From here we wait for the server: disable the buttons and show the spinner
        IsBusy = true;

        // "try ... finally" = whatever happens inside, the "finally" block runs at the end
        try
        {
            // Send the login message and wait for the answer
            AuthResult result = await _api.LoginAsync(Email, Password);

            // The server accepted the email and the password
            if (result.Success)
            {
                Error = null;
                Status = result.Message;

                // Rings the SignedIn bell - if MainWindowViewModel (or anyone else) subscribed with
                // +=, its handler runs right here, synchronously, before this method continues. The
                // ?. guards against calling Invoke when nobody subscribed at all (SignedIn would be null)
                SignedIn?.Invoke(Email, result.Role, result.Token);

            }
            // The login was refused only because the email was never verified
            else if (result.NeedsVerification)
            {
                // The password was right, the account is just still locked - so instead of an error,
                // take the user straight to the screen where they can type the code
                VerificationRequired?.Invoke(Email);
            }

            // Any other refusal (wrong email or password, no connection...)
            else
            {
                // Show the server's message as an error
                Status = null;
                Error = result.Message;
            }
        }
        finally
        {
            // Done waiting: enable the buttons again
            IsBusy = false;
        }
    }


    // Runs when the user presses "Continue with Google"
    private async Task SignInWithGoogleAsync()
    {
        // Clear the messages of the previous attempt
        Status = null;
        Error = null;

        // Disable the buttons and show the spinner
        IsBusy = true;
        try
        {
            // Tell the user to look at the browser window that is about to open
            Status = "Complete the sign-in in your browser...";

            // Open Google's sign-in page and wait for the ID token (throws if the user gives up)
            string idToken = await _google.GetIdTokenAsync();

            // Send the token to our server, which checks it and signs the user in
            AuthResult result = await _api.GoogleLoginAsync(idToken);

            // The server accepted the token
            if (result.Success)
            {
                Error = null;
                Status = result.Message;

                // The email comes from the server's answer, because the user did not type one here
                SignedIn?.Invoke(result.Email, result.Role, result.Token);
            }
            // The server refused
            else
            {
                Status = null;
                Error = result.Message;
            }
        }
        // The browser step failed: closed, timed out after 2 minutes, or the settings are missing
        catch (Exception )
        {
            Status = null;
            Error = "Google sign-in was cancelled or failed.";
        }
        finally
        {
            // Done waiting: enable the buttons again
            IsBusy = false;
        }
    }

}
