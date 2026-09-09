using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NetSim.App.Services;

namespace NetSim.App.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly AuthApiClient _api = new();

    private string _email = string.Empty;
    private string _password = string.Empty;
    private bool _isPasswordVisible;
    private string? _error;
    private string? _status;

    public LoginViewModel()
    {
        SignInCommand = new AsyncRelayCommand(SignInAsync);
        GoToRegisterCommand = new RelayCommand(() => SwitchToRegisterRequested?.Invoke());
    }

    public event Action? SwitchToRegisterRequested;

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

        Status = "Signing in…";
        AuthResult result = await _api.LoginAsync(Email, Password);

        if (result.Success)
        {
            Error = null;
            Status = result.Message;   // "Signed in."
        }
        else
        {
            Status = null;
            Error = result.Message;    // "Wrong email or password."
        }
    }
}
