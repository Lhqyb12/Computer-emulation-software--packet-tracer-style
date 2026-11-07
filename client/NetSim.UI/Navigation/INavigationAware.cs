namespace NetSim.UI.Navigation;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// An optional interface a screen can implement to be told when it is shown and when it is left.
// For example, the Projects screen implements it to load the list every time it is shown
public interface INavigationAware
{
    // Purpose: Called by the navigation service right after this screen is shown.
    // Input:   nothing.
    // Output:  nothing.
    void OnNavigatedTo();

    // Purpose: Called by the navigation service right before another screen replaces this one.
    // Input:   nothing.
    // Output:  nothing.
    void OnNavigatedFrom();
}
