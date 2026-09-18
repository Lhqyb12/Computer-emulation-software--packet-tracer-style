using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NetSim.App.Services;

namespace NetSim.App.ViewModels;

/// <summary>Data and behaviour of the "user management" screen (Admin only).</summary>
public sealed class UsersViewModel : ViewModelBase
{
    private readonly AuthApiClient _api = new();
    // The email of the currently signed-in Admin - sent as the X-User-Email header on every
    // call to the server, so it knows who's asking (see AuthApiClient.GetUsersAsync/DeleteUserAsync)
    private readonly string _callerEmail;

    private bool _isBusy;
    private string? _error;

    public UsersViewModel(string callerEmail)
    {
        _callerEmail = callerEmail;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        GoBackCommand = new RelayCommand(() => BackRequested?.Invoke());
        // Fire-and-forget: loads the list the moment the screen is created, without making the
        // constructor itself async (constructors can't be async in C#)
        _ = LoadAsync();
    }

    /// <summary>Raised when the user clicks "Back" - MainWindowViewModel decides what that means.</summary>
    public event Action? BackRequested;

    /// <summary>The rows currently shown in the table.</summary>
    public ObservableCollection<UserRowViewModel> Users { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand GoBackCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        Error = null;
        try
        {
            var users = await _api.GetUsersAsync(_callerEmail);
            if (users is null)
            {
                Error = "Could not load users. Are you still signed in as an Admin?";
                return;
            }

            Users.Clear();
            foreach (var dto in users)
                Users.Add(new UserRowViewModel(dto, DeleteAsync));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync(UserRowViewModel row)
    {
        Error = null;
        bool ok = await _api.DeleteUserAsync(row.Id, _callerEmail);
        if (ok)
            Users.Remove(row);
        else
            Error = $"Could not delete '{row.Username}'.";
    }
}

/// <summary>One row in the users table.</summary>
public sealed class UserRowViewModel : ViewModelBase
{
    private readonly Func<UserRowViewModel, Task> _delete;

    public UserRowViewModel(UserSummaryDto dto, Func<UserRowViewModel, Task> delete)
    {
        _delete = delete;
        Id = dto.Id;
        Username = dto.Username;
        Email = dto.Email;
        Role = dto.Role;
        LastLoginAt = dto.LastLoginAt;
        DeleteCommand = new AsyncRelayCommand(() => _delete(this));
    }

    public int Id { get; }
    public string Username { get; }
    public string Email { get; }
    public string Role { get; }
    public DateTime? LastLoginAt { get; }

    /// <summary>Human-friendly text for the "last login" column.</summary>
    public string LastLoginDisplay => LastLoginAt.HasValue
        ? LastLoginAt.Value.ToLocalTime().ToString("g")
        : "Never";

    public IRelayCommand DeleteCommand { get; }
}
