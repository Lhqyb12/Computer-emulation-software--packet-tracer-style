using Avalonia;  // AppBuilder
using System;    // STAThread

namespace NetSim.App;

// The entry point of the client: the first code that runs when the program starts
class Program
{
    // Purpose: The program's entry point: builds the Avalonia app and runs it until the window closes.
    // Input:   args - the words typed after the program's name.
    // Output:  nothing.
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    // [STAThread] is an attribute: it tells Windows to run the main thread in the mode a desktop UI needs.
    // Main is the method the operating system calls; args = the words typed after the program's name.
    // It builds the Avalonia application and starts it, which opens the window and stays here until it is closed
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Purpose: Configures the Avalonia application (App class, platform, font, logging).
    // Input:   nothing.
    // Output:  an AppBuilder ready to be started.
    // Avalonia configuration, don't remove; also used by visual designer.
    // Configure<App> = the application class to start is App (see App.axaml.cs)
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            // Choose the right drawing backend for this operating system (Windows / macOS / Linux)
            .UsePlatformDetect()
// "#if DEBUG ... #endif" is an instruction to the compiler itself: the line between them is compiled
// only in Debug builds, and left out of a Release build
#if DEBUG
            // Developer tools (F12): shows the tree of controls on the screen. For development only
            .WithDeveloperTools()
#endif
            // Use the Inter font
            .WithInterFont()
            // Sends Avalonia's internal diagnostic messages to System.Diagnostics.Trace,
            // so they show up in the debugger's output window during development.
            .LogToTrace();
}
