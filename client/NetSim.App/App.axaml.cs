using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NetSim.App.ViewModels;
using NetSim.App.Views;

namespace NetSim.App;

public partial class App : Application
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
            desktop.MainWindow = new MainWindow
            {
                // The window's DataContext is the object its bindings read from.
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
