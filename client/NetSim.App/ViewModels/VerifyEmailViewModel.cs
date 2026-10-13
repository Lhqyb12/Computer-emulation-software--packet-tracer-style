using System;                      // Action
using System.Threading.Tasks;      // Task, async/await
using CommunityToolkit.Mvvm.Input; // RelayCommand, AsyncRelayCommand, IRelayCommand - what a button runs
using NetSim.App.Services;         // AuthApiClient, AuthResult


namespace NetSim.App.ViewModels;

/// <summary>
/// Data and behaviour of the <b>verify your email</b> screen, shown right after registering: the user
/// types the 6-digit code that was emailed to them, which proves the address is real and is theirs.
/// </summary>
public sealed class VerifyEmailViewModel : ViewModelBase
{
    // The object that sends our requests to the server
    private readonly AuthApiClient _api = new();

    // The "backing fields": the real storage behind the public properties further down
    private string _code = string.Empty;   // what is typed in the code box
    private bool _isBusy;                  // true while we wait for the server
    private bool _isSuccess;               // true after the email was verified
    private string? _error;                // the red error text (null = no error)
    private string? _status;               // a neutral status text (null = nothing to show)

    // The email arrives from outside (from the register screen, through MainWindowViewModel) -
    // this screen never asks the user to type it again
    public VerifyEmailViewModel(string email)
    {
        // Keep the email: it is shown on screen and sent with the code
        Email = email;

        // The "Verify" button: runs VerifyAsync, enabled only while not busy
        VerifyCommand = new AsyncRelayCommand(VerifyAsync, () => !IsBusy);

        // The "Resend code" button: runs ResendCodeAsync, enabled only while not busy
        ResendCodeCommand = new AsyncRelayCommand(ResendCodeAsync, () => !IsBusy);

        // The "Back to sign in" link: only raises an event. "?.Invoke()" = call whoever subscribed, if anybody did
        GoToLoginCommand = new RelayCommand(() => SwitchToLoginRequested?.Invoke());
    }

    /// <summary>Raised when the user asks to go back to the sign-in screen.</summary>
    public event Action? SwitchToLoginRequested;

    // ---- Fields ----

    /// <summary>The address the code was sent to. Get-only: it is shown on screen but can't be edited here.</summary>
    public string Email { get; }

    // The text of the code box. SetProperty stores the value and tells the screen it changed;
    // typing also clears the old error
    public string Code
    {
        get => _code;
        set { SetProperty(ref _code, value); Error = null; }
    }

    // True while a request is on its way - disables the buttons and shows a spinner
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            // Store the new value and tell the screen
            SetProperty(ref _isBusy, value);

            // Tell both buttons to ask again whether they are enabled (their rule is "!IsBusy")
            VerifyCommand.NotifyCanExecuteChanged();
            ResendCodeCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>When true the view shows the "email verified" confirmation instead of the code form.</summary>
    public bool IsSuccess
    {
        get => _isSuccess;
        private set => SetProperty(ref _isSuccess, value);
    }

    // ---- Messages ----

    // The error text shown in red. null = no error
    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    // A neutral message, e.g. "A new code was sent". null = nothing to show
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    // ---- Buttons ----

    // The commands the buttons of the screen are bound to. "{ get; }" = set once in the constructor
    public IRelayCommand VerifyCommand { get; }       // the "Verify" button
    public IRelayCommand ResendCodeCommand { get; }   // the "Resend code" button
    public IRelayCommand GoToLoginCommand { get; }    // the "Back to sign in" link

    // Runs when the user presses "Verify"
    private async Task VerifyAsync()
    {
        // Clear the messages of the previous attempt
        Status = null;
        Error = null;

        // A code was typed
        if (string.IsNullOrWhiteSpace(Code))
        {
            Error = "Please enter the code from your email.";
            return;
        }

        // From here we wait for the server: disable the buttons and show the spinner
        IsBusy = true;

        // "try ... finally" = whatever happens inside, the "finally" block runs at the end
        try
        {
            // Trim removes spaces that often sneak in when a code is copied out of an email
            AuthResult result = await _api.VerifyEmailAsync(Email, Code.Trim());

            // The code was right: show the "email verified" confirmation
            if (result.Success)
                IsSuccess = true;
            // The code was refused: show the server's message
            else
                Error = result.Message; // "Invalid or expired code." / "Could not reach the server..."

        }
        finally
        {
            // Done waiting: enable the buttons again
            IsBusy = false;
        }
    }

    // Runs when the user presses "Resend code"
    private async Task ResendCodeAsync()
    {
        // Clear the messages of the previous attempt
        Status = null;
        Error = null;

        // Disable the buttons and show the spinner
        IsBusy = true;
        try
        {
            // Ask the server to email a new code
            AuthResult result = await _api.ResendVerificationAsync(Email);

            // The server answered (it gives the same answer whether or not a code really went out)
            if (result.Success)
                Status = "A new code was sent. If it doesn't arrive, wait a minute and try again.";
            // The server could not be reached
            else
                Error = result.Message;

        }
        finally
        {
            // Done waiting: enable the buttons again
            IsBusy = false;
        }
    }
}
