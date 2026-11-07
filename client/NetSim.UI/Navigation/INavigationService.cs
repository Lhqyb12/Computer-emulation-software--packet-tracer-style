using System;              // EventHandler
using NetSim.UI.Common;    // ViewModelBase

namespace NetSim.UI.Navigation;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// What the screens may ask from the navigation: show a screen, go back, and which screen is shown.
// Screens are named by their view model type, never by their view, so this stays free of Avalonia
public interface INavigationService
{
    // The screen shown now, or null before the first navigation
    ViewModelBase? CurrentViewModel { get; }

    // true when there is a previous screen to go back to
    bool CanGoBack { get; }

    // Raised every time CurrentViewModel changes (also after GoBack)
    event EventHandler? CurrentViewModelChanged;

    // Purpose: Shows a new screen of this type, and remembers the current one for "Back".
    // Input:   TViewModel - the screen type, e.g. NavigateTo<ProjectsViewModel>().
    // Output:  nothing.
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;

    // Purpose: Returns to the previous screen.
    // Input:   nothing.
    // Output:  nothing (does nothing if there is no previous screen).
    void GoBack();
}
