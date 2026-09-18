using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NetSim.App.Composition;
using NetSim.App.ViewModels;
using NetSim.App.Views;

namespace NetSim.App;

public partial class App : Avalonia.Application
{
    // Loads App.axaml (styles, resources, the ViewLocator).
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // Runs once, after Avalonia is ready. We create the main window here.
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Builds the simulator workspace's DI container (Application + UI services, see
            // Composition/CompositionRoot.cs). Login/register stay outside it. Runs exactly once,
            // before any window exists, so the container is fully built by the time anything
            // could ask it for a service.
            var services = CompositionRoot.BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                // The window's DataContext is the object its bindings read from. The container gets
                // handed straight into MainWindowViewModel's constructor - it's the _services field
                // ShowSignedIn later calls GetRequiredService<ShellViewModel>() on.
                DataContext = new MainWindowViewModel(services),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
