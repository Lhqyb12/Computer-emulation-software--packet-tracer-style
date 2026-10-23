using Microsoft.Extensions.DependencyInjection;
using NetSim.UI.Dialogs;
using NetSim.UI.Navigation;
using NetSim.UI.ViewModels;

namespace NetSim.UI.DependencyInjection;

/// <summary>
/// Composition entry point for the UI layer. Future Views and ViewModels are registered
/// here as they are introduced.
/// </summary>
public static class UIServiceCollectionExtensions
{
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
        services.AddScoped<IDialogHost, DialogHost>();
        services.AddScoped<IDialogService, DialogService>();

        services.AddScoped<ShellViewModel>();

        services.AddTransient<HomeViewModel>();
        services.AddTransient<ProjectsViewModel>();


        return services;
    }
}
