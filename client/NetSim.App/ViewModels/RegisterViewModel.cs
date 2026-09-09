using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NetSim.App.Services;

namespace NetSim.App.ViewModels;

public sealed class RegisterViewModel : ViewModelBase
{
    private readonly AuthApiClient _api = new();

    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _isPasswordVisible;
    private string? _error;
    private string? _status;

    public RegisterViewModel()
    {
        CreateAccountCommand = new AsyncRelayCommand(CreateAccountAsync);
        GoToLoginCommand = new RelayCommand(() => SwitchToLoginRequested?.Invoke());
    }

    public event Action? SwitchToLoginRequested;

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

    public IRelayCommand CreateAccountCommand { get; }
    public IRelayCommand GoToLoginCommand { get; }

    private async Task CreateAccountAsync()
    {
        Status = null;
        Error = null;

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

        if (Password != ConfirmPassword)
        {
            Error = "Passwords do not match.";
            return;
        }

        Status = "Creating account…";
        AuthResult result = await _api.RegisterAsync(Username, Email, Password);

        if (result.Success)
        {
            Error = null;
            Status = result.Message;   // "Account created."
        }
        else
        {
            Status = null;
            Error = result.Message;    // כללי השרת: סיסמה חלשה, אימייל תפוס...
        }
    }
}
