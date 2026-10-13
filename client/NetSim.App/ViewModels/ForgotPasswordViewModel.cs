using System;                      // Action
using System.Linq;                 // Any - checks whether at least one character matches a rule
using System.Threading.Tasks;      // Task, async/await
using CommunityToolkit.Mvvm.Input; // RelayCommand, AsyncRelayCommand, IRelayCommand - what a button runs
using NetSim.App.Services;         // AuthApiClient, AuthResult

namespace NetSim.App.ViewModels;

/// <summary>
/// Data and behaviour of the "forgot password" flow: request a code by email, then use it
/// (with a new password) to actually reset it. Talks to the server via <see cref="AuthApiClient"/>.
/// </summary>
public sealed class ForgotPasswordViewModel : ViewModelBase
{
    // The object that sends our requests to the server
    private readonly AuthApiClient _api = new();

    // The "backing fields": the real storage behind the public properties further down
    private string _email = string.Empty;             // what is typed in the email box
    private string _code = string.Empty;              // what is typed in the code box
    private string _newPassword = string.Empty;       // what is typed in the "new password" box
    private string _confirmPassword = string.Empty;   // what is typed in the "confirm password" box
    private bool _isPasswordVisible;                  // true = show the passwords as readable text
    private bool _isBusy;                             // true while we wait for the server
    private bool _isCodeSent;                         // true after step 1 (a code was requested)
    private bool _isSuccess;                          // true after step 2 (the password was replaced)
    private string? _error;                           // the red error text (null = no error)
    private string? _status;                          // a neutral status text (null = nothing to show)

    // The constructor: connects every button to the code it runs
    public ForgotPasswordViewModel()
    {
        // Step 1 button: runs SendCodeAsync, enabled only while not busy
        SendCodeCommand = new AsyncRelayCommand(SendCodeAsync, () => !IsBusy);

        // Step 2 button: runs ResetPasswordAsync, enabled only while not busy
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => !IsBusy);

        // The "Back to sign in" link: only raises an event. "?.Invoke()" = call whoever subscribed, if anybody did
        GoToLoginCommand = new RelayCommand(() => SwitchToLoginRequested?.Invoke());
    }

    /// <summary>Raised when the user asks to go back to the sign-in screen.</summary>
    public event Action? SwitchToLoginRequested;

    // ---- Fields ----

    // The text of the email box. SetProperty stores the value and tells the screen it changed;
    // typing also clears the old error
    public string Email
    {
        get => _email;
        set { SetProperty(ref _email, value); Error = null; }
    }

    // The text of the code box (the 6 digits from the email)
    public string Code
    {
        get => _code;
        set { SetProperty(ref _code, value); Error = null; }
    }

    // The text of the "new password" box. Every typed letter refreshes the checklist
    public string NewPassword
    {
        get => _newPassword;
        set { SetProperty(ref _newPassword, value); Error = null; RaiseChecks(); }
    }

    // The text of the "confirm password" box. Every typed letter refreshes the "passwords match" check
    public string ConfirmPassword
    {
        get => _confirmPassword;
        set { SetProperty(ref _confirmPassword, value); Error = null; RaiseChecks(); }
    }

    // true = the passwords are shown as readable text; switched by the "eye" button
    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
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
            SendCodeCommand.NotifyCanExecuteChanged();
            ResetPasswordCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>True once a code has been requested - the view then shows the code+new-password form.</summary>
    public bool IsCodeSent
    {
        get => _isCodeSent;
        private set
        {
            // Store the new value and tell the screen
            SetProperty(ref _isCodeSent, value);

            // ShowResetForm is computed from this value, so the screen is told by hand that it changed too
            OnPropertyChanged(nameof(ShowResetForm));
        }
    }

    /// <summary>True once the password has actually been reset - the view shows the final confirmation.</summary>
    public bool IsSuccess
    {
        get => _isSuccess;
        private set
        {
            // Store the new value and tell the screen
            SetProperty(ref _isSuccess, value);

            // ShowResetForm depends on this value as well
            OnPropertyChanged(nameof(ShowResetForm));
        }
    }

    /// <summary>True only between "code sent" and "reset completed" - drives which of the three panels shows.</summary>
    public bool ShowResetForm => IsCodeSent && !IsSuccess;

    // ---- Live password checklist (mirrors the server rules - same idea as RegisterViewModel) ----
    // Each of these is calculated from the current text every time the screen asks for it

    // At least 8 characters
    public bool HasMinLength => NewPassword.Length >= 8;

    // At least one digit
    public bool HasNumber => NewPassword.Any(char.IsDigit);

    // At least one character that is neither a letter nor a digit
    public bool HasSpecial => NewPassword.Any(c => !char.IsLetterOrDigit(c));

    // Both password boxes hold the same text (and the second one is not empty)
    public bool PasswordsMatch => ConfirmPassword.Length > 0 && NewPassword == ConfirmPassword;

    // Something was typed in the second box and it is different - the screen shows a warning
    public bool ShowPasswordMismatch => ConfirmPassword.Length > 0 && NewPassword != ConfirmPassword;

    // The five properties above have no "set", so the screen cannot know by itself that they changed.
    // This tells it, by name, to read each of them again
    private void RaiseChecks()
    {
        OnPropertyChanged(nameof(HasMinLength));
        OnPropertyChanged(nameof(HasNumber));
        OnPropertyChanged(nameof(HasSpecial));
        OnPropertyChanged(nameof(PasswordsMatch));
        OnPropertyChanged(nameof(ShowPasswordMismatch));
    }

    // ---- Messages ----

    // The error text shown in red. null = no error
    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    // A neutral message. null = nothing to show
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    // ---- Buttons ----

    // The commands the buttons of the screen are bound to. "{ get; }" = set once in the constructor
    public IRelayCommand SendCodeCommand { get; }        // step 1: "Send code"
    public IRelayCommand ResetPasswordCommand { get; }   // step 2: "Reset password"
    public IRelayCommand GoToLoginCommand { get; }       // "Back to sign in"

    // Step 1: runs when the user presses "Send code"
    private async Task SendCodeAsync()
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

        // From here we wait for the server: disable the buttons and show the spinner
        IsBusy = true;

        // "try ... finally" = whatever happens inside, the "finally" block runs at the end
        try
        {
            // ForgotPasswordAsync always reports success by design - the server never reveals whether
            // the email is actually registered (see AuthService.ForgotPasswordAsync)
            await _api.ForgotPasswordAsync(Email);

            // The same careful wording as the server uses
            Status = "If an account exists for that email, a reset code was sent.";

            // Switch the screen to the "code + new password" form
            IsCodeSent = true;
        }
        finally
        {
            // Done waiting: enable the buttons again
            IsBusy = false;
        }
    }

    // Step 2: runs when the user presses "Reset password"
    private async Task ResetPasswordAsync()
    {
        // Clear the error of the previous attempt
        Error = null;

        // A code was typed
        if (string.IsNullOrWhiteSpace(Code))
        {
            Error = "Please enter the code from your email.";
            return;
        }

        // Both password boxes hold the same text
        if (NewPassword != ConfirmPassword)
        {
            Error = "Passwords do not match.";
            return;
        }

        // Disable the buttons and show the spinner
        IsBusy = true;
        try
        {
            // Send the email, the code and the new password, and wait for the answer
            AuthResult result = await _api.ResetPasswordAsync(Email, Code, NewPassword);

            // The password was replaced: show the final confirmation
            if (result.Success)
                IsSuccess = true;
            // The server refused: show its message
            else
                Error = result.Message; // "Invalid or expired code." / a password policy message
        }
        finally
        {
            // Done waiting: enable the buttons again
            IsBusy = false;
        }
    }
}
