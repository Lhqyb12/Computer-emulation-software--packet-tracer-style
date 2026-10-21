using System;                          // Action, Func, DateTime
using System.Collections.ObjectModel;  // ObservableCollection - a list that tells the screen when it changes
using System.Threading.Tasks;          // Task, async/await
using CommunityToolkit.Mvvm.Input;     // RelayCommand, AsyncRelayCommand, IRelayCommand - what a button runs
using NetSim.App.Services;             // AuthApiClient, UserSummaryDto

namespace NetSim.App.ViewModels;

/// <summary>Data and behaviour of the "user management" screen (Admin only).</summary>
public sealed class UsersViewModel : ViewModelBase
{
    // The object that sends our requests to the server
    private readonly AuthApiClient _api = new();

    // The JWT of the currently signed-in Admin - sent inside every message this screen sends,
    // so the server knows who's asking (see AuthApiClient.GetUsersAsync/DeleteUserAsync)

    private readonly string _token;


    // The "backing fields" behind the two properties below
    private bool _isBusy;      // true while the list is loading
    private string? _error;    // the red error text (null = no error)

    // Purpose: Builds the user-management screen's view model and starts loading the user list.
    // Input:   token - the admin's JWT from login.
    // Output:  none (a constructor) - a new UsersViewModel.
    // The constructor. token = the JWT the server gave at login
    public UsersViewModel(string token)
    {
        // Keep the token for the requests
        _token = token;

        // The "Refresh" button reads the list again
        RefreshCommand = new AsyncRelayCommand(LoadAsync);

        // The "Back" button only raises an event. "?.Invoke()" = call whoever subscribed, if anybody did
        GoBackCommand = new RelayCommand(() => BackRequested?.Invoke());

        // Fire-and-forget: loads the list the moment the screen is created, without making the
        // constructor itself async (constructors can't be async in C#)
        _ = LoadAsync();
    }

    /// <summary>Raised when the user clicks "Back" - MainWindowViewModel decides what that means.</summary>
    public event Action? BackRequested;

    /// <summary>The rows currently shown in the table.</summary>
    public ObservableCollection<UserRowViewModel> Users { get; } = new();

    // True while the list is loading - the screen shows a spinner
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    // The error text shown in red. null = no error
    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    // The commands the buttons of the screen are bound to
    public IRelayCommand RefreshCommand { get; }   // the "Refresh" button
    public IRelayCommand GoBackCommand { get; }    // the "Back" button

    // Purpose: Asks the server for all the users and fills the table.
    // Input:   nothing (it uses the saved token).
    // Output:  nothing (a Task). Fills Users, or sets Error if the list could not be loaded.
    // Asks the server for all the users and fills the table
    private async Task LoadAsync()
    {
        // Show the spinner and clear the old error
        IsBusy = true;
        Error = null;

        // "try ... finally" = whatever happens inside, the "finally" block runs at the end
        try
        {
            // Send the "get-users" message with our token. null = the list could not be loaded
            var users = await _api.GetUsersAsync( _token);

            // The server refused (not an admin, or the token expired) or could not be reached
            if (users is null)
            {
                Error = "Could not load users. Are you still signed in as an Admin?";
                return;
            }

            // Empty the table on screen before filling it again
            Users.Clear();

            // One row for every user. Each row gets DeleteAsync, so its own Delete button can call it
            foreach (var dto in users)
                Users.Add(new UserRowViewModel(dto, DeleteAsync));
        }
        finally
        {
            // Done loading: hide the spinner (this runs even after the "return" above)
            IsBusy = false;
        }
    }

    // Purpose: Asks the server to delete the user of one row and removes the row on success.
    // Input:   row - the table row whose Delete button was pressed.
    // Output:  nothing (a Task). Sets Error if the server refused.
    // Deletes the user of one row. Called by the Delete button of that row
    private async Task DeleteAsync(UserRowViewModel row)
    {
        // Clear the old error
        Error = null;

        // Send the "delete-user" message with the user's id and our token. ok = the server deleted the user
        bool ok = await _api.DeleteUserAsync(row.Id, _token);

        // Deleted: remove the row from the table on screen as well
        if (ok)
            Users.Remove(row);
        // Refused (for example, an admin trying to delete themselves): show an error
        else
            Error = $"Could not delete '{row.Username}'.";
    }
}

/// <summary>One row in the users table.</summary>
public sealed class UserRowViewModel : ViewModelBase
{
    // The method to call when this row's Delete button is pressed.
    // "Func<UserRowViewModel, Task>" = a method that receives a row and returns a Task (an async method)
    private readonly Func<UserRowViewModel, Task> _delete;

    // Purpose: Builds one table row from the user's details.
    // Input:   dto - the user's details from the server; delete - the method that deletes a row.
    // Output:  none (a constructor) - a new UserRowViewModel.
    // The constructor. dto = the user's details as the server sent them,
    // delete = UsersViewModel.DeleteAsync, handed in so the row can ask its parent to delete it
    public UserRowViewModel(UserSummaryDto dto, Func<UserRowViewModel, Task> delete)
    {
        // Keep the delete method
        _delete = delete;

        // Copy the details the table shows
        Id = dto.Id;
        Username = dto.Username;
        Email = dto.Email;
        Role = dto.Role;
        LastLoginAt = dto.LastLoginAt;

        // The Delete button of this row: call the delete method with this row itself ("this")
        DeleteCommand = new AsyncRelayCommand(() => _delete(this));
    }

    // The details of the user. "{ get; }" = set once in the constructor, then only read
    public int Id { get; }                   // the user's id in the database
    public string Username { get; }          // the display name
    public string Email { get; }             // the email address
    public string Role { get; }              // "User" or "Admin"
    public DateTime? LastLoginAt { get; }    // the last sign-in (UTC), or null if never

    /// <summary>Human-friendly text for the "last login" column.</summary>
    // A date and time in this computer's time zone ("g" = the short general format), or the word "Never"
    public string LastLoginDisplay => LastLoginAt.HasValue
        ? LastLoginAt.Value.ToLocalTime().ToString("g")
        : "Never";

    // The command the Delete button of this row is bound to
    public IRelayCommand DeleteCommand { get; }
}
