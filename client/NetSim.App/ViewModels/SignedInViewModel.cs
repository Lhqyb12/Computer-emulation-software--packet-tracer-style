using System;                      // Action
using CommunityToolkit.Mvvm.Input; // RelayCommand, IRelayCommand - what a button runs

namespace NetSim.App.ViewModels;

/// <summary>
/// Shown after a successful sign-in. The real product would hand off to the
/// simulator workspace here – for now it just confirms the session.
/// </summary>
public sealed class SignedInViewModel : ViewModelBase
{
    // Purpose: Builds the signed-in screen's view model and its Sign out command.
    // Input:   email - the address that signed in.
    // Output:  none (a constructor) - a new SignedInViewModel.
    // The constructor. email = the address that signed in
    public SignedInViewModel(string email)
    {
        // Keep the email so the screen can show it
        Email = email;

        // The "Sign out" button only raises an event. "?.Invoke()" = call whoever subscribed, if anybody did
        SignOutCommand = new RelayCommand(() => SignOutRequested?.Invoke());
    }

    // Raised when the user presses "Sign out"; whoever owns navigation decides what happens next
    public event Action? SignOutRequested;

    // The email shown on the screen. "{ get; }" = set once in the constructor, then only read
    public string Email { get; }

    // The command the "Sign out" button is bound to
    public IRelayCommand SignOutCommand { get; }
}
