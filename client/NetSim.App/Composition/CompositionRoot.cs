using System;                                    // IServiceProvider
using Microsoft.Extensions.DependencyInjection;  // ServiceCollection, AddSingleton, BuildServiceProvider
using NetSim.Application.DependencyInjection;    // AddApplicationServices
using NetSim.Application.Persistence;            // IProjectRepository
using NetSim.App.Persistence;                    // ServerProjectRepository, ServerClassroomService
using NetSim.Application.Services;               // IClassroomService
using NetSim.UI.DependencyInjection;             // AddUIServices

namespace NetSim.App.Composition;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

/// <summary>
/// Builds the dependency-injection container for the simulator workspace (everything shown
/// after sign-in). Stage 1: Application + UI services only, with an in-memory
/// <see cref="IProjectRepository"/> standing in for real persistence - there is no
/// NetSim.Infrastructure/database layer yet. Login/register stay outside this container; they
/// are plain view models constructed directly by <see cref="ViewModels.MainWindowViewModel"/>.
/// </summary>
// "static" = there is no object to create; the method is called directly on the class name
public static class CompositionRoot
{
    // Purpose: Registers all the workspace services and builds the DI container.
    // Input:   nothing.
    // Output:  the IServiceProvider that creates the workspace objects on request.
    // Builds the container and returns it. Called once, from App.OnFrameworkInitializationCompleted
    public static IServiceProvider BuildServiceProvider()
    {
        // ServiceCollection is just a list of "recipes" at this point - nothing gets built yet,
        // we're only registering how to build things when someone later asks for them
        var services = new ServiceCollection();

        // Each of these pulls in a whole batch of recipes written elsewhere - AddApplicationServices
        // from NetSim.Application (canvas/device/project services), AddUIServices from NetSim.UI
        // (ShellViewModel and the rest of the workspace screens). This file doesn't know the details
        // of what's being registered, it just imports both layers' service registrations wholesale
        services
            .AddApplicationServices()
            .AddUIServices();

        // "Whoever asks for IProjectRepository gets a ServerProjectRepository": the projects are stored
        // on the server, in the database, and belong to the signed-in user.
        // Scoped, not Singleton: it needs ICurrentUser (one per sign-in) for the token. A Singleton
        // would be built once, before anyone signed in, and would keep an empty token forever.
        // (InMemoryProjectRepository is kept in the project - it can be put back here for testing
        // without a server.)
        services.AddScoped<IProjectRepository, ServerProjectRepository>();

        // "Whoever asks for IClassroomService gets a ServerClassroomService": the classes, and their rules,
        // live on the server. Scoped for the same reason as above - it needs ICurrentUser's token.
        // (To try the Classes screen without a server, register ClassroomService instead, together with
        // IClassroomRepository -> InMemoryClassroomRepository as a Singleton.)
        services.AddScoped<IClassroomService, ServerClassroomService>();

        // Turns the recipe list into an actual live container that GetRequiredService<T>() can pull
        // built objects out of - this is the IServiceProvider MainWindowViewModel later uses
        return services.BuildServiceProvider();
    }
}
