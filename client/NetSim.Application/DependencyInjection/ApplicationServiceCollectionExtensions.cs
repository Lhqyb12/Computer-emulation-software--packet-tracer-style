using Microsoft.Extensions.DependencyInjection;
using NetSim.Application.Services;
using NetSim.Application.State;

namespace NetSim.Application.DependencyInjection;

/// <summary>
/// Composition entry point for the application layer. Future use-case and workflow
/// services (project management, simulation orchestration, ...) are registered here as
/// they are introduced.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Scoped, not Singleton (a change from the original): the "current project" and its
        // unsaved-changes flag belong to the signed-in user, exactly like the Shell and the
        // navigation history. MainWindowViewModel opens a new scope on sign-in and disposes it on
        // sign-out, so the next user starts with no project open instead of the previous user's.
        services.AddScoped<IApplicationState, ApplicationState>();
        services.AddScoped<IProjectService, ProjectService>();

        return services;
    }
}
