using System;
using CommunityToolkit.Mvvm.Input;

namespace NetSim.App.ViewModels;

/// <summary>
/// Holds the data and behaviour of the <b>create account</b> screen.
/// Display-only for now: it validates the fields and shows a message.
/// </summary>
public sealed class RegisterViewModel : ViewModelBase
{
    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _isPasswordVisible;
    private string? _error;
    private string? _status;

    public RegisterViewModel()
    {
        CreateAccountCommand = new RelayCommand(CreateAccount);
        GoToLoginCommand = new RelayCommand(() => SwitchToLoginRequested?.Invoke());
    }

    /// <summary>Raised when the user clicks "Sign in" – the window swaps back to the login screen.</summary>
    public event Action? SwitchToLoginRequested;

    // ---- Fields ----

    public string Username
    {
        get => _username;
        set { SetProperty(ref _username, value); Error = null; }
    }

    public string Email
    {
        get => _email;
        set { SetProperty(ref _email, value); Error = null; }
    }

    public string Password
    {
        get => _password;
        set { SetProperty(ref _password, value); Error = null; }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set { SetProperty(ref _confirmPassword, value); Error = null; }
    }

    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
    }

    // ---- Messages ----

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    // ---- Buttons ----

    public IRelayCommand CreateAccountCommand { get; }
    public IRelayCommand GoToLoginCommand { get; }

    private void CreateAccount()
    {
        Status = null;

        if (string.IsNullOrWhiteSpace(Username))
        {
            Error = "Please choose a username.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            Error = "Please enter a valid email address.";
            return;
        }

        if (Password.Length < 8)
        {
            Error = "Password must be at least 8 characters.";
            return;
        }

        if (Password != ConfirmPassword)
        {
            Error = "Passwords do not match.";
            return;
        }

        Error = null;
        Status = "Account details look valid. (Registration isn't connected yet.)";
    }
}
