using System;
using Microsoft.Extensions.DependencyInjection;
using NetSim.Application.DependencyInjection;
using NetSim.Application.Persistence;
using NetSim.App.Persistence;
using NetSim.UI.DependencyInjection;

namespace NetSim.App.Composition;

/// <summary>
/// Builds the dependency-injection container for the simulator workspace (everything shown
/// after sign-in). Stage 1: Application + UI services only, with an in-memory
/// <see cref="IProjectRepository"/> standing in for real persistence - there is no
/// NetSim.Infrastructure/database layer yet. Login/register stay outside this container; they
/// are plain view models constructed directly by <see cref="ViewModels.MainWindowViewModel"/>.
/// </summary>
public static class CompositionRoot
{
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

        // The one recipe actually written for this repo: "whoever asks for IProjectRepository gets
        // an InMemoryProjectRepository". Stage 1 stand-in - there's no real database yet, so this
        // just keeps projects in RAM for the lifetime of the app. AddSingleton means one shared
        // instance for the whole app, not a new one per request.
        services.AddSingleton<IProjectRepository, InMemoryProjectRepository>();

        // Turns the recipe list into an actual live container that GetRequiredService<T>() can pull
        // built objects out of - this is the IServiceProvider MainWindowViewModel later uses
        return services.BuildServiceProvider();
    }
}
