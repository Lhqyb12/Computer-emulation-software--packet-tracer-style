using Avalonia;                                  // Application - the base class of an Avalonia app
using Avalonia.Controls.ApplicationLifetimes;    // IClassicDesktopStyleApplicationLifetime - a normal desktop app with a main window
using Avalonia.Markup.Xaml;                      // AvaloniaXamlLoader - reads .axaml files

namespace NetSim.Server.Dashboard;

// The desktop (Avalonia) application that lives inside the server process - the Super Admin screen.
// It is the server-side twin of the client's App class, but it shows what the server is doing.
// "partial" = the other half of this class is generated from DashboardApp.axaml at build time
public partial class DashboardApp : Application
{
    // The server's DI container, handed over by Program.cs. This is the bridge between the two
    // halves of the process: through it the window can reach the database
    private readonly IServiceProvider _services;

    // Purpose: Builds the dashboard application and keeps the server's DI container.
    // Input:   services - the server's DI container, from Program.cs.
    // Output:  none (a constructor) - a new DashboardApp.
    // The constructor: keep the container
    public DashboardApp(IServiceProvider services)
    {
        _services = services;
    }

    // Purpose: Loads the dashboard's theme and styles from DashboardApp.axaml.
    // Input:   nothing.
    // Output:  nothing.
    // Loads DashboardApp.axaml (the theme and styles).
    // "override" = we replace the empty version that Application has
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // Purpose: Creates the dashboard window with its view model and makes it the main window.
    // Input:   nothing (Avalonia calls it once at start-up).
    // Output:  nothing.
    // Runs once, after Avalonia is ready. We create the dashboard window here.
    public override void OnFrameworkInitializationCompleted()
    {
        // Check that we run as a normal desktop application; if so, "desktop" is the object that manages its windows
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Create the window and make it the main one (closing it ends the application)
            desktop.MainWindow = new DashboardWindow
            {
                // The window's DataContext is the object its bindings read from
                DataContext = new DashboardViewModel(_services),
            };
        }

        // Let the base class finish its own part of the start-up
        base.OnFrameworkInitializationCompleted();
    }
}
