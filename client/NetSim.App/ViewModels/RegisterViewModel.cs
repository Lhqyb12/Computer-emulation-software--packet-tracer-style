using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NetSim.App.Services;

namespace NetSim.App.ViewModels;

/// <summary>
/// Data and behaviour of the <b>create account</b> screen. Talks to the server via <see cref="AuthApiClient"/>.
/// </summary>
public sealed class RegisterViewModel : ViewModelBase
{
    private readonly AuthApiClient _api = new();

    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _isPasswordVisible;
    private bool _isBusy;
    private bool _isSuccess;
    private string? _error;
    private string? _status;

    public RegisterViewModel()
    {
        CreateAccountCommand = new AsyncRelayCommand(CreateAccountAsync, () => !IsBusy);
        GoToLoginCommand = new RelayCommand(() => SwitchToLoginRequested?.Invoke());
    }

    /// <summary>Raised when the user asks to go back to the sign-in screen.</summary>
    public event Action? SwitchToLoginRequested;

    // ---- Fields ----

    public string Username
    {
        get => _username;
        set { SetProperty(ref _username, value); Error = null; RaiseChecks(); }
    }

    public string Email
    {
        get => _email;
        set { SetProperty(ref _email, value); Error = null; }
    }

    public string Password
    {
        get => _password;
        set { SetProperty(ref _password, value); Error = null; RaiseChecks(); }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set { SetProperty(ref _confirmPassword, value); Error = null; RaiseChecks(); }
    }

    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            SetProperty(ref _isBusy, value);
            CreateAccountCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>When true the view shows the "account created" confirmation instead of the form.</summary>
    public bool IsSuccess
    {
        get => _isSuccess;
        private set => SetProperty(ref _isSuccess, value);
    }

    // ---- Live password checklist (mirrors the server rules) ----

    public bool HasMinLength => Password.Length >= 8;
    public bool HasNumber => Password.Any(char.IsDigit);
    public bool HasSpecial => Password.Any(c => !char.IsLetterOrDigit(c));
    public bool NotContainsUsername =>
        Password.Length > 0 &&
        (Username.Trim().Length < 3 || !Password.Contains(Username.Trim(), StringComparison.OrdinalIgnoreCase));

    public bool PasswordsMatch => ConfirmPassword.Length > 0 && Password == ConfirmPassword;
    public bool ShowPasswordMismatch => ConfirmPassword.Length > 0 && Password != ConfirmPassword;

    private void RaiseChecks()
    {
        OnPropertyChanged(nameof(HasMinLength));
        OnPropertyChanged(nameof(HasNumber));
        OnPropertyChanged(nameof(HasSpecial));
        OnPropertyChanged(nameof(NotContainsUsername));
        OnPropertyChanged(nameof(PasswordsMatch));
        OnPropertyChanged(nameof(ShowPasswordMismatch));
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

        IsBusy = true;
        try
        {
            AuthResult result = await _api.RegisterAsync(Username, Email, Password);

            if (result.Success)
            {
                Error = null;
                Status = null;
                IsSuccess = true;
            }
            else
            {
                Status = null;
                Error = result.Message; // server rules: weak password, email taken, ...
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
