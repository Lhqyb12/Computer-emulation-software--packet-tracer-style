using CommunityToolkit.Mvvm.ComponentModel;

namespace NetSim.App.ViewModels;

/// <summary>
/// Base class for every view model.
/// <para>
/// It inherits <see cref="ObservableObject"/>, which provides <c>SetProperty(...)</c>:
/// a helper that stores a new value AND notifies the UI so bindings refresh.
/// </para>
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}
