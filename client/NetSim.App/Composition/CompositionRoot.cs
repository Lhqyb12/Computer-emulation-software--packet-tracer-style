using System;                                    // IServiceProvider
using Microsoft.Extensions.DependencyInjection;  // ServiceCollection, AddSingleton, BuildServiceProvider
using NetSim.UI.DependencyInjection;             // AddUIServices

namespace NetSim.App.Composition;

/// <summary>
/// Builds the dependency-injection container for the simulator workspace (everything shown
/// after sign-in). Login/register stay outside this container; they are plain view models
/// constructed directly by <see cref="ViewModels.MainWindowViewModel"/>.
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

        // Pulls in a whole batch of recipes written elsewhere - AddUIServices from NetSim.UI
        // (ShellViewModel and the rest of the workspace screens). This file doesn't know the details
        // of what's being registered, it just imports the layer's service registrations wholesale
        services
            .AddUIServices();

        // Turns the recipe list into an actual live container that GetRequiredService<T>() can pull
        // built objects out of - this is the IServiceProvider MainWindowViewModel later uses
        return services.BuildServiceProvider();
    }
}
