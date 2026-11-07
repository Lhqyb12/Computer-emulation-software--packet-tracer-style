using System;                      // Action, StringComparison
using System.Linq;                 // Any - checks whether at least one character matches a rule
using System.Threading.Tasks;      // Task, async/await
using CommunityToolkit.Mvvm.Input; // RelayCommand, AsyncRelayCommand, IRelayCommand - what a button runs
using NetSim.App.Services;         // AuthApiClient, AuthResult

namespace NetSim.App.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

/// <summary>
/// Data and behaviour of the <b>create account</b> screen. Talks to the server via <see cref="AuthApiClient"/>.
/// </summary>
public sealed class RegisterViewModel : ViewModelBase
{
    // The object that sends our requests to the server
    private readonly AuthApiClient _api = new();

    // The "backing fields": the real storage behind the public properties further down
    private string _username = string.Empty;          // what is typed in the username box
    private string _email = string.Empty;             // what is typed in the email box
    private string _password = string.Empty;          // what is typed in the password box
    private string _confirmPassword = string.Empty;   // what is typed in the "confirm password" box
    private bool _isPasswordVisible;                  // true = show the passwords as readable text
    private bool _isBusy;                             // true while we wait for the server
    private string? _error;                           // the red error text (null = no error)
    private string? _status;                          // a neutral status text (null = nothing to show)

    // Purpose: Builds the create-account screen's view model and connects each button to its command.
    // Input:   nothing.
    // Output:  none (a constructor) - a new RegisterViewModel with empty boxes.
    // The constructor: connects every button to the code it runs
    public RegisterViewModel()
    {
        // The "Create account" button: runs CreateAccountAsync, and is enabled only while not busy
        CreateAccountCommand = new AsyncRelayCommand(CreateAccountAsync, () => !IsBusy);

        // The "Sign in" link: only raises an event. "?.Invoke()" = call whoever subscribed, if anybody did
        GoToLoginCommand = new RelayCommand(() => SwitchToLoginRequested?.Invoke());
    }

    /// <summary>Raised when the user asks to go back to the sign-in screen.</summary>
    public event Action? SwitchToLoginRequested;


    /// <summary>Raised after the account was created, carrying the email that now has to be verified.</summary>
    public event Action<string>? VerificationRequired;


    // ---- Fields ----

    // The text of the username box. SetProperty stores the value and tells the screen it changed;
    // typing also clears the old error and refreshes the checklist (one rule depends on the username)
    public string Username
    {
        get => _username;
        set { SetProperty(ref _username, value); Error = null; RaiseChecks(); }   // store it, tell the screen, clear the old error, and re-check the password rules
    }

    // The text of the email box. It has no part in the password checklist, so no RaiseChecks here
    public string Email
    {
        get => _email;
        set { SetProperty(ref _email, value); Error = null; }   // store it, tell the screen, and clear the old error (the user is fixing it)
    }

    // The text of the password box. Every typed letter refreshes the checklist
    public string Password
    {
        get => _password;
        set { SetProperty(ref _password, value); Error = null; RaiseChecks(); }   // store it, tell the screen, clear the old error, and re-check the password rules
    }

    // The text of the "confirm password" box. Every typed letter refreshes the "passwords match" check
    public string ConfirmPassword
    {
        get => _confirmPassword;
        set { SetProperty(ref _confirmPassword, value); Error = null; RaiseChecks(); }   // store it, tell the screen, clear the old error, and re-check the password rules
    }

    // true = the passwords are shown as readable text; switched by the "eye" button
    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);   // store the new value and tell the screen
    }

    // True while a request is on its way - disables the button and shows a spinner
    public bool IsBusy
    {
        get => _isBusy;
        private set   // only this class can change it
        {
            // Store the new value and tell the screen
            SetProperty(ref _isBusy, value);

            // Tell the button to ask again whether it is enabled (its rule is "!IsBusy")
            CreateAccountCommand.NotifyCanExecuteChanged();   // the CreateAccount button checks again whether it may run (turns on / off)
        }
    }



    // ---- Live password checklist (mirrors the server rules) ----
    // Each of these is calculated from the current text every time the screen asks for it.
    // The screen shows a tick next to every rule that is true

    // At least 8 characters
    public bool HasMinLength => Password.Length >= 8;

    // At least one digit
    public bool HasNumber => Password.Any(char.IsDigit);

    // At least one character that is neither a letter nor a digit
    public bool HasSpecial => Password.Any(c => !char.IsLetterOrDigit(c));

    // The password does not contain the username. A username shorter than 3 letters is not checked
    // (the same rule as on the server). An empty password does not get a tick
    public bool NotContainsUsername =>
        Password.Length > 0 &&
        (Username.Trim().Length < 3 || !Password.Contains(Username.Trim(), StringComparison.OrdinalIgnoreCase));

    // Both password boxes hold the same text (and the second one is not empty)
    public bool PasswordsMatch => ConfirmPassword.Length > 0 && Password == ConfirmPassword;

    // Something was typed in the second box and it is different - the screen shows a warning
    public bool ShowPasswordMismatch => ConfirmPassword.Length > 0 && Password != ConfirmPassword;

    // Purpose: Tells the screen to re-read all six password-checklist properties.
    // Input:   nothing.
    // Output:  nothing.
    // The six properties above have no "set", so the screen cannot know by itself that they changed.
    // This tells it, by name, to read each of them again. nameof(X) = the name of X as text
    private void RaiseChecks()
    {
        OnPropertyChanged(nameof(HasMinLength));   // tell the screen that HasMinLength may have changed
        OnPropertyChanged(nameof(HasNumber));   // tell the screen that HasNumber may have changed
        OnPropertyChanged(nameof(HasSpecial));   // tell the screen that HasSpecial may have changed
        OnPropertyChanged(nameof(NotContainsUsername));   // tell the screen that NotContainsUsername may have changed
        OnPropertyChanged(nameof(PasswordsMatch));   // tell the screen that PasswordsMatch may have changed
        OnPropertyChanged(nameof(ShowPasswordMismatch));   // tell the screen that ShowPasswordMismatch may have changed
    }

    // ---- Messages ----

    // The error text shown in red. null = no error
    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);   // only this class sets it; store it and tell the screen
    }

    // A neutral message. null = nothing to show
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);   // only this class sets it; store it and tell the screen
    }

    // ---- Buttons ----

    // The commands the buttons of the screen are bound to. "{ get; }" = set once in the constructor
    public IRelayCommand CreateAccountCommand { get; }   // the "Create account" button
    public IRelayCommand GoToLoginCommand { get; }       // the "Sign in" link

    // Purpose: Checks the typed fields, asks the server to create the account and acts on the answer.
    // Input:   nothing (it reads Username, Email, Password and ConfirmPassword).
    // Output:  nothing (a Task). On success raises VerificationRequired with the email; otherwise sets Error.
    // Runs when the user presses "Create account"
    private async Task CreateAccountAsync()
    {
        // Clear the messages of the previous attempt
        Status = null;   // hide the status line
        Error = null;   // clear the old error message

        // Three quick checks before bothering the server. Each one shows an error and stops.
        // A username was typed
        if (string.IsNullOrWhiteSpace(Username))
        {
            Error = "Please choose a username.";
            return;   // stop here - nothing more to do
        }

        // An email was typed and it has an "@"
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            Error = "Please enter a valid email address.";
            return;   // stop here - nothing more to do
        }

        // Both password boxes hold the same text
        if (Password != ConfirmPassword)
        {
            Error = "Passwords do not match.";
            return;   // stop here - nothing more to do
        }

        // From here we wait for the server: disable the button and show the spinner
        IsBusy = true;

        // "try ... finally" = whatever happens inside, the "finally" block runs at the end
        try
        {
            // Send the register message and wait for the answer
            AuthResult result = await _api.RegisterAsync(Username, Email, Password);

            // The account was created
            if (result.Success)
            {
                Error = null;   // clear the old error message
                Status = null;   // hide the status line

                // Move to the "enter your code" screen, taking the email along
                VerificationRequired?.Invoke(Email);

            }
            // The server refused
            else
            {
                Status = null;   // hide the status line
                Error = result.Message; // server rules: weak password, email taken, ...
            }
        }
        finally   // "finally" always runs at the end - after success and after an error
        {
            // Done waiting: enable the button again
            IsBusy = false;
        }
    }
}
