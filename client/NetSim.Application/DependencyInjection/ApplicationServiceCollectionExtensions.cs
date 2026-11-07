using Microsoft.Extensions.DependencyInjection;   // IServiceCollection, AddScoped
using NetSim.Application.Services;                // IProjectService, ProjectService, IClassroomService, ClassroomService
using NetSim.Application.State;                   // IApplicationState, ApplicationState, ICurrentUser, CurrentUser
using NetSim.Application.Canvas;                  // IDevicePlacementState, DevicePlacementState


namespace NetSim.Application.DependencyInjection;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Where the Application layer writes its "recipes" into the DI container
public static class ApplicationServiceCollectionExtensions
{
    // Purpose: Adds all the Application layer's recipes to the container's list.
    // Input:   services - the list of recipes ("this" = it can be called as services.AddApplicationServices()).
    // Output:  the same list, so another call can be chained after it.
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Scoped, not Singleton (a change from the original): the "current project" and its
        // unsaved-changes flag belong to the signed-in user, exactly like the Shell and the
        // navigation history. MainWindowViewModel opens a new scope on sign-in and disposes it on
        // sign-out, so the next user starts with no project open instead of the previous user's.
        services.AddScoped<IApplicationState, ApplicationState>();   // "whoever asks for IApplicationState gets ApplicationState"
        services.AddScoped<IProjectService, ProjectService>();       // the project rules

        // Who is signed in - one per sign-in, filled by MainWindowViewModel right after the scope opens
        services.AddScoped<ICurrentUser, CurrentUser>();

        // The device waiting to be placed on the canvas - one per sign-in, like the open project it belongs to
        services.AddScoped<IDevicePlacementState, DevicePlacementState>();


        // Classes (IClassroomService) are NOT registered here any more: their rules now run on the server,
        // so NetSim.App decides which implementation to use (see CompositionRoot) - the same way it
        // decides where projects are stored

        // Return the list, so another Add...() can follow
        return services;
    }
}
