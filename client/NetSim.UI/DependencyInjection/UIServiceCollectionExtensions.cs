using Microsoft.Extensions.DependencyInjection;
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

        services.AddScoped<ShellViewModel>();

        services.AddTransient<HomeViewModel>();

        return services;
    }
}
