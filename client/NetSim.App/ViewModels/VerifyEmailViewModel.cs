using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NetSim.App.Services;


namespace NetSim.App.ViewModels;

/// <summary>
/// Data and behaviour of the <b>verify your email</b> screen, shown right after registering: the user
/// types the 6-digit code that was emailed to them, which proves the address is real and is theirs.
/// </summary>
public sealed class VerifyEmailViewModel : ViewModelBase
{
    private readonly AuthApiClient _api = new();

    private string _code = string.Empty;
    private bool _isBusy;
    private bool _isSuccess;
    private string? _error;
    private string? _status;

    // The email arrives from outside (from the register screen, through MainWindowViewModel) -
    // this screen never asks the user to type it again
    public VerifyEmailViewModel(string email)
    {
        Email = email;

        VerifyCommand = new AsyncRelayCommand(VerifyAsync, () => !IsBusy);
        ResendCodeCommand = new AsyncRelayCommand(ResendCodeAsync, () => !IsBusy);
        GoToLoginCommand = new RelayCommand(() => SwitchToLoginRequested?.Invoke());
    }

    /// <summary>Raised when the user asks to go back to the sign-in screen.</summary>
    public event Action? SwitchToLoginRequested;

    // ---- Fields ----

    /// <summary>The address the code was sent to. Get-only: it is shown on screen but can't be edited here.</summary>
    public string Email { get; }

    public string Code
    {
        get => _code;
        set { SetProperty(ref _code, value); Error = null; }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            SetProperty(ref _isBusy, value);
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

    public IRelayCommand VerifyCommand { get; }
    public IRelayCommand ResendCodeCommand { get; }
    public IRelayCommand GoToLoginCommand { get; }

    private async Task VerifyAsync()
    {
        Status = null;
        Error = null;

        if (string.IsNullOrWhiteSpace(Code))
        {
            Error = "Please enter the code from your email.";
            return;
        }

        IsBusy = true;
        try
        {
            // Trim removes spaces that often sneak in when a code is copied out of an email
            AuthResult result = await _api.VerifyEmailAsync(Email, Code.Trim());

            if (result.Success)
                IsSuccess = true;
            else
                Error = result.Message; // "Invalid or expired code." / "Could not reach the server..."

        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResendCodeAsync()
    {
        Status = null;
        Error = null;

        IsBusy = true;
        try
        {
            AuthResult result = await _api.ResendVerificationAsync(Email);

            if (result.Success)
                Status = "A new code was sent. If it doesn't arrive, wait a minute and try again.";
            else
                Error = result.Message;

        }
        finally
        {
            IsBusy = false;
        }
    }
}
