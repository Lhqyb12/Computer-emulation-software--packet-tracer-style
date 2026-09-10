using System;
using CommunityToolkit.Mvvm.Input;

namespace NetSim.App.ViewModels;

/// <summary>
/// Shown after a successful sign-in. The real product would hand off to the
/// simulator workspace here – for now it just confirms the session.
/// </summary>
public sealed class SignedInViewModel : ViewModelBase
{
    public SignedInViewModel(string email)
    {
        Email = email;
        SignOutCommand = new RelayCommand(() => SignOutRequested?.Invoke());
    }

    public event Action? SignOutRequested;

    public string Email { get; }

    public IRelayCommand SignOutCommand { get; }
}
