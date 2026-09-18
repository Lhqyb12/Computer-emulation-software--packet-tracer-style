namespace NetSim.UI.Navigation;

/// <summary>
/// Optional lifecycle hook a ViewModel can implement to be notified when the
/// <see cref="INavigationService"/> navigates to or away from it. Implementing this interface is
/// opt-in; most ViewModels will not need it.
/// </summary>
public interface INavigationAware
{
    void OnNavigatedTo();

    void OnNavigatedFrom();
}
