using CommunityToolkit.Mvvm.ComponentModel;  // ObservableObject

namespace NetSim.App.ViewModels;

/// <summary>
/// Base class for every view model.
/// <para>
/// It inherits <see cref="ObservableObject"/>, which provides <c>SetProperty(...)</c>:
/// a helper that stores a new value AND notifies the UI so bindings refresh.
/// </para>
/// </summary>
// "abstract" = it is forbidden to create an object directly from this class; it exists only to be inherited.
// The body is empty on purpose: everything it offers comes from ObservableObject
public abstract class ViewModelBase : ObservableObject
{
}
