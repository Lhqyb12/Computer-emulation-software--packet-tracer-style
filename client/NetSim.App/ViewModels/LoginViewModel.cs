using System;
using CommunityToolkit.Mvvm.Input;

namespace NetSim.App.ViewModels;

/// <summary>
/// Holds the data and behaviour of the <b>sign in</b> screen.
/// This phase is display-only: it validates the fields and shows a message,
/// but does not talk to any server yet.
/// </summary>
public sealed class LoginViewModel : ViewModelBase
{
    private string _email = string.Empty;
    private string _password = string.Empty;
    private bool _isPasswordVisible;
    private string? _error;
    private string? _status;

    public LoginViewModel()
    {
        SignInCommand = new RelayCommand(SignIn);
        GoToRegisterCommand = new RelayCommand(() => SwitchToRegisterRequested?.Invoke());
    }

    /// <summary>Raised when the user clicks "Create one" – the window swaps in the register screen.</summary>
    public event Action? SwitchToRegisterRequested;

    // ---- Fields the user types into (two-way bound in the View) ----

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

    /// <summary>Bound to the "Show / Hide" toggle next to the password box.</summary>
    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
    }

    // ---- Messages shown back to the user ----

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

    public IRelayCommand SignInCommand { get; }
    public IRelayCommand GoToRegisterCommand { get; }

    private void SignIn()
    {
        Status = null;

        if (string.IsNullOrWhiteSpace(Email))
        {
            Error = "Please enter your email.";
            return;
        }

        if (!Email.Contains('@'))
        {
            Error = "Please enter a valid email address.";
            return;
        }

        if (string.IsNullOrEmpty(Password))
        {
            Error = "Please enter your password.";
            return;
        }

        Error = null;
        Status = "Looks good. (Sign-in isn't connected yet.)";
    }
}
