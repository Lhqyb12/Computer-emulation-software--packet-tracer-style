using Avalonia.Controls;                        // UserControl
using Avalonia.Controls.ApplicationLifetimes;   // IClassicDesktopStyleApplicationLifetime - can close the app
using Avalonia.Interactivity;                   // RoutedEventArgs - details of a click

namespace NetSim.UI.Views;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The code half of ShellView.axaml. "partial" = the other half is built from the .axaml file
public partial class ShellView : UserControl
{
    // Purpose: Builds the Shell's view.
    // Input:   nothing.
    // Output:  none (a constructor).
    public ShellView()
    {
        // Load ShellView.axaml and build its controls
        InitializeComponent();
    }

    // Purpose: The menu's "Exit" item - closes the program.
    // Input:   sender - what was clicked; e - details of the click (neither is used).
    // Output:  nothing.
    // "Avalonia.Application" is spelled out fully because the word "Application" alone could be
    // mixed up with our NetSim.Application project
    private void OnExitClicked(object? sender, RoutedEventArgs e)
    {
        // If the program runs as a normal desktop app, "desktop" is the object that manages its windows
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Close the program
            desktop.Shutdown();
        }
    }
}
