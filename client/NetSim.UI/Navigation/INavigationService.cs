using System;
using NetSim.UI.Common;

namespace NetSim.UI.Navigation;

/// <summary>
/// Abstraction over screen navigation. Screens are identified by their ViewModel type, never by
/// View type, so this service stays independent of any concrete Avalonia control.
/// </summary>
public interface INavigationService
{
    /// <summary>The ViewModel currently on screen, or null before the first navigation.</summary>
    ViewModelBase? CurrentViewModel { get; }

    /// <summary>Whether <see cref="GoBack"/> has anywhere to go.</summary>
    bool CanGoBack { get; }

    /// <summary>Raised whenever <see cref="CurrentViewModel"/> changes, including on <see cref="GoBack"/>.</summary>
    event EventHandler? CurrentViewModelChanged;

    /// <summary>
    /// Navigates to a freshly resolved instance of <typeparamref name="TViewModel"/>, pushing the
    /// current ViewModel onto the back stack.
    /// </summary>
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;

    /// <summary>Returns to the previous ViewModel on the back stack, if any.</summary>
    void GoBack();
}
