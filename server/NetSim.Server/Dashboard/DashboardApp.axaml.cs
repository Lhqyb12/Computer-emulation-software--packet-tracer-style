using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace NetSim.Server.Dashboard;

// The desktop (Avalonia) application that lives inside the server process - the Super Admin screen.
// It is the server-side twin of the client's App class, but it shows what the server is doing
public partial class DashboardApp : Application
{
    // The server's DI container, handed over by Program.cs. This is the bridge between the two
    // halves of the process: through it the window can reach the database
    private readonly IServiceProvider _services;

    public DashboardApp(IServiceProvider services)
    {
        _services = services;
    }

    // Loads DashboardApp.axaml (the theme and styles).
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // Runs once, after Avalonia is ready. We create the dashboard window here.
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new DashboardWindow
            {
                // The window's DataContext is the object its bindings read from
                DataContext = new DashboardViewModel(_services),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
