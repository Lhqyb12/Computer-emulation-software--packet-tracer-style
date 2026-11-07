using Microsoft.Extensions.DependencyInjection;   // IServiceCollection, AddScoped, AddTransient
using NetSim.UI.Dialogs;                          // IDialogHost, DialogHost, IDialogService, DialogService
using NetSim.UI.Navigation;                       // INavigationService, NavigationService
using NetSim.UI.ViewModels;                       // the screens

namespace NetSim.UI.DependencyInjection;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Where the UI layer writes its "recipes" into the DI container
public static class UIServiceCollectionExtensions
{
    // Purpose: Adds all the UI layer's recipes to the container's list.
    // Input:   services - the list of recipes ("this" = it can be called as services.AddUIServices()).
    // Output:  the same list, so another call can be chained after it.
    public static IServiceCollection AddUIServices(this IServiceCollection services)
    {
        // Scoped, not Singleton: one per signed-in session. MainWindowViewModel opens a new scope on
        // sign-in and disposes it on sign-out, so the next user gets a fresh Shell and an empty
        // navigation history instead of the previous user's
        services.AddScoped<INavigationService, NavigationService>();

        // One dialog host/overlay per signed-in session - see UI/Dialogs/IDialogHost.cs.
        // The individual dialog ViewModels (NewProjectDialogViewModel, ...) are constructed
        // directly by DialogService (they take result/cancel callbacks, not DI dependencies),
        // so they are not registered here.
        services.AddScoped<IDialogHost, DialogHost>();          // the one host of this sign-in
        services.AddScoped<IDialogService, DialogService>();    // opens dialogs into that host

        // The Shell - one per sign-in
        services.AddScoped<ShellViewModel>();

        // The screens - Transient = a new one every time we navigate to it, so it shows fresh data
        services.AddTransient<HomeViewModel>();   // the Home screen
        services.AddTransient<ProjectsViewModel>();   // the Projects screen
        services.AddTransient<ClassroomsViewModel>();   // the Classes screen
        services.AddTransient<DeviceLibraryViewModel>();   // the Device Library panel (inside the workspace)
        services.AddTransient<NetworkWorkspaceViewModel>();   // the Network Workspace screen



        // Return the list, so another Add...() can follow
        return services;
    }
}
