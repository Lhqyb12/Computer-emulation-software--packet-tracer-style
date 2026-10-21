using Avalonia;                                // The Avalonia UI framework
using Avalonia.Controls.ApplicationLifetimes;  // IClassicDesktopStyleApplicationLifetime - a normal desktop app with a main window
using Avalonia.Markup.Xaml;                    // AvaloniaXamlLoader - reads .axaml files
using NetSim.App.Composition;                  // CompositionRoot
using NetSim.App.ViewModels;                   // MainWindowViewModel
using NetSim.App.Views;                        // MainWindow



namespace NetSim.App;

// The client application itself. Avalonia creates one object of this class when the program starts.
// "partial" = the other half of this class is generated from App.axaml at build time
public partial class App : Avalonia.Application
{
    // Purpose: Loads the application's styles and resources from App.axaml.
    // Input:   nothing.
    // Output:  nothing.
    // Loads App.axaml (styles, resources, the ViewLocator).
    // "override" = we replace the empty version that Application has
    public override void Initialize()
    {
        // Read App.axaml and apply everything it declares to this application
        AvaloniaXamlLoader.Load(this);
    }

     // Purpose: Builds the workspace DI container and opens the main window with its view model.

    // Input:   nothing (Avalonia calls it once at start-up).
    // Output:  nothing.
    // Runs once, after Avalonia is ready. We create the main window here.
    public override void OnFrameworkInitializationCompleted()
    {
        // Check that we run as a normal desktop application; if so, "desktop" is the object that manages its windows
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
                        // Builds the simulator workspace's DI container (Application + UI services, see
            // Composition/CompositionRoot.cs). Login/register stay outside it. Runs exactly once,
            // before any window exists, so the container is fully built by the time anything
            // could ask it for a service.
            // The container is a "box" that knows how to create every object the workspace needs
            var services = CompositionRoot.BuildServiceProvider();

            // Create the main window (Views/MainWindow.axaml) and make it the application's main window
            desktop.MainWindow = new MainWindow
            {
                // The window's DataContext is the object its bindings read from. The container gets
                // handed straight into MainWindowViewModel's constructor - it's the _services field
                // ShowSignedIn later calls GetRequiredService<ShellViewModel>() on.
                DataContext = new MainWindowViewModel(services),
            };




        }

        // Let the base class finish its own part of the start-up
        base.OnFrameworkInitializationCompleted();
    }



}
