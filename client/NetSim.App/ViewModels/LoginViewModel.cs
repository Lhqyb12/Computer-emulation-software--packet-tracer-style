using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NetSim.App.Services;

namespace NetSim.App.ViewModels;

/// <summary>
/// Data and behaviour of the <b>sign in</b> screen. Talks to the server via <see cref="AuthApiClient"/>.
/// </summary>
public sealed class LoginViewModel : ViewModelBase
{
    private readonly AuthApiClient _api = new();

    private string _email = string.Empty;
    private string _password = string.Empty;
    private bool _isPasswordVisible;
    private bool _isBusy;
    private string? _error;
    private string? _status;

    public LoginViewModel()
    {
        SignInCommand = new AsyncRelayCommand(SignInAsync, () => !IsBusy);
        GoToRegisterCommand = new RelayCommand(() => SwitchToRegisterRequested?.Invoke());
    }

    /// <summary>Raised when the user asks to go to the register screen.</summary>
    public event Action? SwitchToRegisterRequested;

    /// <summary>Raised after a successful sign-in, carrying the email that signed in.</summary>
    public event Action<string>? SignedIn;

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

    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
    }

    /// <summary>True while a request is in flight – disables the button and shows a spinner.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            SetProperty(ref _isBusy, value);
            SignInCommand.NotifyCanExecuteChanged();
        }
    }

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

    public IRelayCommand SignInCommand { get; }
    public IRelayCommand GoToRegisterCommand { get; }

    private async Task SignInAsync()
    {
        Status = null;
        Error = null;

        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            Error = "Please enter a valid email address.";
            return;
        }

        if (string.IsNullOrEmpty(Password))
        {
            Error = "Please enter your password.";
            return;
        }

        IsBusy = true;
        try
        {
            AuthResult result = await _api.LoginAsync(Email, Password);

            if (result.Success)
            {
                Error = null;
                Status = result.Message;
                SignedIn?.Invoke(Email);
            }
            else
            {
                Status = null;
                Error = result.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
