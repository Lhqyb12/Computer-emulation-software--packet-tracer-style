using Avalonia;
using System;

namespace NetSim.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread] //a command for windows to run the project as desktop ui needs- Attribute
    public static void Main(string[] args) => BuildAvaloniaApp() //main function, shows ui windows
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>() //app because its the name of my main class for this application- at app.axaml.cs
            .UsePlatformDetect() //windows/mac/linux
#if DEBUG //for the complier itself- runs only in debug mode
            .WithDeveloperTools() //runs only in develop mode-tree of controls
#endif
            .WithInterFont() //font
            .LogToTrace(); // Sends Avalonia's internal diagnostic messages to System.Diagnostics.Trace,
            // so they show up in the debugger's output window during development.
}
