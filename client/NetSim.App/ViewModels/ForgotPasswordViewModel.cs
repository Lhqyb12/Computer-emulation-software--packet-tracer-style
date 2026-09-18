using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NetSim.App.Services;

namespace NetSim.App.ViewModels;

/// <summary>
/// Data and behaviour of the "forgot password" flow: request a code by email, then use it
/// (with a new password) to actually reset it. Talks to the server via <see cref="AuthApiClient"/>.
/// </summary>
public sealed class ForgotPasswordViewModel : ViewModelBase
{
    private readonly AuthApiClient _api = new();

    private string _email = string.Empty;
    private string _code = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _isPasswordVisible;
    private bool _isBusy;
    private bool _isCodeSent;
    private bool _isSuccess;
    private string? _error;
    private string? _status;

    public ForgotPasswordViewModel()
    {
        SendCodeCommand = new AsyncRelayCommand(SendCodeAsync, () => !IsBusy);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => !IsBusy);
        GoToLoginCommand = new RelayCommand(() => SwitchToLoginRequested?.Invoke());
    }

    /// <summary>Raised when the user asks to go back to the sign-in screen.</summary>
    public event Action? SwitchToLoginRequested;

    // ---- Fields ----

    public string Email
    {
        get => _email;
        set { SetProperty(ref _email, value); Error = null; }
    }

    public string Code
    {
        get => _code;
        set { SetProperty(ref _code, value); Error = null; }
    }

    public string NewPassword
    {
        get => _newPassword;
        set { SetProperty(ref _newPassword, value); Error = null; RaiseChecks(); }
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
            SetProperty(ref _isCodeSent, value);
            OnPropertyChanged(nameof(ShowResetForm));
        }
    }

    /// <summary>True once the password has actually been reset - the view shows the final confirmation.</summary>
    public bool IsSuccess
    {
        get => _isSuccess;
        private set
        {
            SetProperty(ref _isSuccess, value);
            OnPropertyChanged(nameof(ShowResetForm));
        }
    }

    /// <summary>True only between "code sent" and "reset completed" - drives which of the three panels shows.</summary>
    public bool ShowResetForm => IsCodeSent && !IsSuccess;

    // ---- Live password checklist (mirrors the server rules - same idea as RegisterViewModel) ----

    public bool HasMinLength => NewPassword.Length >= 8;
    public bool HasNumber => NewPassword.Any(char.IsDigit);
    public bool HasSpecial => NewPassword.Any(c => !char.IsLetterOrDigit(c));
    public bool PasswordsMatch => ConfirmPassword.Length > 0 && NewPassword == ConfirmPassword;
    public bool ShowPasswordMismatch => ConfirmPassword.Length > 0 && NewPassword != ConfirmPassword;

    private void RaiseChecks()
    {
        OnPropertyChanged(nameof(HasMinLength));
        OnPropertyChanged(nameof(HasNumber));
        OnPropertyChanged(nameof(HasSpecial));
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

    public IRelayCommand SendCodeCommand { get; }
    public IRelayCommand ResetPasswordCommand { get; }
    public IRelayCommand GoToLoginCommand { get; }

    private async Task SendCodeAsync()
    {
        Status = null;
        Error = null;

        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            Error = "Please enter a valid email address.";
            return;
        }

        IsBusy = true;
        try
        {
            // ForgotPasswordAsync always reports success by design - the server never reveals whether
            // the email is actually registered (see AuthService.ForgotPasswordAsync)
            await _api.ForgotPasswordAsync(Email);
            Status = "If an account exists for that email, a reset code was sent.";
            IsCodeSent = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResetPasswordAsync()
    {
        Error = null;

        if (string.IsNullOrWhiteSpace(Code))
        {
            Error = "Please enter the code from your email.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            Error = "Passwords do not match.";
            return;
        }

        IsBusy = true;
        try
        {
            AuthResult result = await _api.ResetPasswordAsync(Email, Code, NewPassword);

            if (result.Success)
                IsSuccess = true;
            else
                Error = result.Message; // "Invalid or expired code." / a password policy message
        }
        finally
        {
            IsBusy = false;
        }
    }
}
