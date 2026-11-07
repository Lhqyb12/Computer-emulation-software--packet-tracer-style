using System;                                    // IServiceProvider, EventHandler, EventArgs
using System.Collections.Generic;                // Stack<T> - "last in, first out", like a pile of plates
using Microsoft.Extensions.DependencyInjection;  // GetRequiredService<T>() - ask the DI container for an object
using NetSim.UI.Common;                          // ViewModelBase

namespace NetSim.UI.Navigation;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Changes the screen shown in the middle of the Shell, and remembers the previous screens for "Back".
// Screens are built by the DI container, so every screen gets the services its constructor asks for
public sealed class NavigationService : INavigationService
{
    // The DI container of this sign-in's scope - builds the screens we navigate to
    private readonly IServiceProvider _serviceProvider;

    // The history: every time we move forward, the old screen is pushed on top;
    // "Back" takes the top one off again
    private readonly Stack<ViewModelBase> _backStack = new();

    // Purpose: Builds the navigation service.
    // Input:   serviceProvider - the DI container (of this sign-in's scope), passed in by the container itself.
    // Output:  none (a constructor).
    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;   // keep the container, to build screens later
    }

    // The screen shown now, or null before the first navigation. Only this class may change it
    public ViewModelBase? CurrentViewModel { get; private set; }

    // true when the history has a screen to go back to
    public bool CanGoBack => _backStack.Count > 0;

    // Raised every time CurrentViewModel changes. The Shell listens to it
    public event EventHandler? CurrentViewModelChanged;

    // Purpose: Shows a new screen of the given type, and remembers the current one for "Back".
    // Input:   TViewModel - the type of screen to show, written in <>, e.g. NavigateTo<HomeViewModel>().
    // Output:  nothing.
    // "where TViewModel : ViewModelBase" = only screen types are allowed
    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        // Ask the container to build the screen (with all the services its constructor needs)
        var next = _serviceProvider.GetRequiredService<TViewModel>();

        // Show it, and push the current screen into the history
        SetCurrent(next, pushCurrentToHistory: true);
    }

    // Purpose: Returns to the previous screen.
    // Input:   nothing.
    // Output:  nothing. Does nothing if there is no previous screen.
    public void GoBack()
    {
        // Nowhere to go back to
        if (!CanGoBack)
        {
            return;   // stop here - nothing more to do
        }

        // Take the last screen off the top of the history
        var previous = _backStack.Pop();

        // Show it - without pushing the current one, or Back + Back would jump between the same two screens
        SetCurrent(previous, pushCurrentToHistory: false);
    }

    // Purpose: Does the actual switch of screens, for both NavigateTo and GoBack.
    // Input:   next - the screen to show; pushCurrentToHistory - true = remember the current screen for "Back".
    // Output:  nothing (raises CurrentViewModelChanged).
    private void SetCurrent(ViewModelBase next, bool pushCurrentToHistory)
    {
        // The screen shown until now
        var previous = CurrentViewModel;

        // The very same object is already shown - nothing to do
        if (ReferenceEquals(previous, next))
        {
            return;   // stop here - nothing more to do
        }

        // If the old screen wants to know it is being left, tell it.
        // "is INavigationAware previousAware" = "if it implements the interface, call it previousAware"
        if (previous is INavigationAware previousAware)
        {
            previousAware.OnNavigatedFrom();
        }

        // Remember the old screen for "Back" (on the first navigation there is no old screen)
        if (pushCurrentToHistory && previous is not null)
        {
            _backStack.Push(previous);
        }

        // The new screen is now the current one
        CurrentViewModel = next;

        // If the new screen wants to know it is shown, tell it (e.g. the Projects screen loads its list here)
        if (next is INavigationAware nextAware)
        {
            nextAware.OnNavigatedTo();
        }

        // Tell the listeners (the Shell) that the screen changed. "?." = only if someone listens
        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
    }
}
